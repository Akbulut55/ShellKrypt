using ShellKrypt.Core.Items;
using ShellKrypt.Application.QuickFill;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class QuickFillEntryService
{
    private static QuickFillEntryPayload ToPayload(QuickFillEntryInput input)
    {
        var target = NormalizeTarget(input.Target);
        if (string.IsNullOrWhiteSpace(target.ProcessName))
            throw new InvalidOperationException("Target process is required.");

        var fields = NormalizeFields(input.Fields).ToArray();
        if (fields.Length == 0)
            throw new InvalidOperationException("Add at least one Quick Fill field.");

        return new QuickFillEntryPayload(
            Name: NormalizeRequired(input.Name, "Entry name is required."),
            Category: string.IsNullOrWhiteSpace(input.Category) ? "Other" : input.Category.Trim(),
            Enabled: input.Enabled,
            Target: target,
            Fields: fields,
            PressEnterAfterFill: input.PressEnterAfterFill,
            Notes: NormalizeText(input.Notes),
            SequenceSteps: NormalizeSequenceSteps(input.SequenceSteps, fields).ToArray());
    }

    private static QuickFillEntry ToEntry(VaultItemHeader header, QuickFillEntryPayload payload)
    {
        var fields = NormalizeFields(payload.Fields).ToArray();
        return new QuickFillEntry(
            Id: header.Id,
            Name: payload.Name,
            Category: string.IsNullOrWhiteSpace(payload.Category) ? "Other" : payload.Category,
            Enabled: payload.Enabled,
            Target: NormalizeTarget(payload.Target),
            Fields: fields,
            PressEnterAfterFill: payload.PressEnterAfterFill,
            Notes: payload.Notes,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc,
            SequenceSteps: NormalizeSequenceSteps(payload.SequenceSteps, fields).ToArray());
    }

    private static QuickFillTargetRule NormalizeTarget(QuickFillTargetRule? target)
        => target is null
            ? new QuickFillTargetRule("", "")
            : new QuickFillTargetRule(NormalizeProcessName(target.ProcessName), NormalizeText(target.WindowTitleContains));

    private static IEnumerable<QuickFillField> NormalizeFields(IEnumerable<QuickFillField>? fields)
    {
        var order = 0;
        foreach (var field in fields ?? Array.Empty<QuickFillField>())
        {
            if (!Enum.IsDefined(typeof(QuickFillFieldKind), field.Kind) ||
                !Enum.IsDefined(typeof(QuickFillFieldSourceKind), field.SourceKind))
            {
                continue;
            }

            var label = NormalizeText(field.Label);
            if (string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException("Every Quick Fill field needs a label.");

            yield return new QuickFillField(
                Id: string.IsNullOrWhiteSpace(field.Id) ? Guid.NewGuid().ToString("N") : field.Id.Trim(),
                Label: label,
                Kind: field.Kind,
                IsSensitive: field.IsSensitive || IsSensitiveKind(field.Kind),
                SortOrder: field.SortOrder <= 0 ? order : field.SortOrder,
                SourceKind: field.SourceKind,
                Value: field.Value ?? "",
                LinkedItemId: NormalizeText(field.LinkedItemId),
                LinkedFieldId: NormalizeText(field.LinkedFieldId),
                LinkedFieldName: NormalizeFieldName(field.LinkedFieldName));
            order++;
        }
    }

    private static IEnumerable<QuickFillSequenceStep> NormalizeSequenceSteps(
        IEnumerable<QuickFillSequenceStep>? steps,
        IReadOnlyList<QuickFillField> fields)
        => QuickFillSequencePreviewer.NormalizeSequenceSteps(fields, steps?.ToArray());

    private static bool IsSensitiveKind(QuickFillFieldKind kind)
        => kind is QuickFillFieldKind.Password or QuickFillFieldKind.Secret or QuickFillFieldKind.Otp;

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);

        return trimmed;
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

    private static string NormalizeProcessName(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }

    private static string NormalizeFieldName(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
}

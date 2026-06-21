using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.QuickFill;

public static class QuickFillSequencePreviewer
{
    public static IReadOnlyList<string> BuildPreview(QuickFillEntry entry)
        => NormalizeSequenceSteps(entry.Fields, entry.SequenceSteps)
            .Select(step => DescribeStep(step, entry.Fields))
            .ToArray();

    public static IReadOnlyList<QuickFillField> ChooseFillFields(QuickFillEntry entry)
    {
        var fields = entry.Fields.OrderBy(field => field.SortOrder).ToArray();
        var username = fields.FirstOrDefault(field => field.Kind == QuickFillFieldKind.Username);
        var secret = fields.FirstOrDefault(field => field.Kind is QuickFillFieldKind.Password or QuickFillFieldKind.Secret or QuickFillFieldKind.Otp);
        if (username is not null && secret is not null)
            return [username, secret];

        return fields;
    }

    public static IReadOnlyList<QuickFillSequenceStep> NormalizeSequenceSteps(QuickFillEntry entry)
        => NormalizeSequenceSteps(entry.Fields, entry.SequenceSteps);

    public static IReadOnlyList<QuickFillSequenceStep> NormalizeSequenceSteps(
        IReadOnlyList<QuickFillField> fields,
        IReadOnlyList<QuickFillSequenceStep>? steps)
    {
        var validFieldIds = fields.Select(field => field.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<QuickFillSequenceStep>();
        var order = 0;
        foreach (var step in (steps ?? Array.Empty<QuickFillSequenceStep>()).OrderBy(step => step.SortOrder))
        {
            var fieldId = step.FieldId?.Trim() ?? "";
            if (!Enum.IsDefined(typeof(QuickFillSequenceStepKind), step.Kind))
                continue;

            if (step.Kind == QuickFillSequenceStepKind.Field && !validFieldIds.Contains(fieldId))
                continue;

            if (step.Kind == QuickFillSequenceStepKind.Keystroke &&
                !Enum.IsDefined(typeof(QuickFillKeystrokeKind), step.Keystroke))
            {
                continue;
            }

            normalized.Add(step with
            {
                Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString("N") : step.Id.Trim(),
                SortOrder = order++,
                FieldId = fieldId,
                Text = step.Text ?? "",
                DelayMilliseconds = Math.Clamp(step.DelayMilliseconds, 0, 10_000)
            });
        }

        return normalized.Count > 0 ? normalized : BuildDefaultSequence(fields);
    }

    public static IReadOnlyList<QuickFillSequenceStep> BuildDefaultSequence(IReadOnlyList<QuickFillField> fields)
    {
        var chosen = ChooseFillFields(new QuickFillEntry("", "", "", true, new QuickFillTargetRule("", ""), fields, false, "", "", ""));
        if (chosen.Count == 0)
            return Array.Empty<QuickFillSequenceStep>();

        var steps = new List<QuickFillSequenceStep>();
        for (var i = 0; i < chosen.Count; i++)
        {
            if (i > 0)
                steps.Add(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Keystroke, steps.Count, "", QuickFillKeystrokeKind.Tab, "", 0));
            steps.Add(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Field, steps.Count, chosen[i].Id, QuickFillKeystrokeKind.Tab, "", 0));
        }

        return steps;
    }

    private static string DescribeField(QuickFillField field)
        => field.IsSensitive
            ? $"{field.Label} (masked)"
            : field.Label;

    private static string DescribeStep(QuickFillSequenceStep step, IReadOnlyList<QuickFillField> fields)
        => step.Kind switch
        {
            QuickFillSequenceStepKind.Field => DescribeField(fields.FirstOrDefault(field => string.Equals(field.Id, step.FieldId, StringComparison.OrdinalIgnoreCase))
                ?? new QuickFillField("", "Field", QuickFillFieldKind.Text, false, 0, QuickFillFieldSourceKind.Owned, "", "", "", "")),
            QuickFillSequenceStepKind.Keystroke => step.Keystroke == QuickFillKeystrokeKind.Enter ? "[Enter]" : "[Tab]",
            QuickFillSequenceStepKind.LiteralText => string.IsNullOrWhiteSpace(step.Text) ? "Text" : "Text",
            QuickFillSequenceStepKind.Delay => $"Delay {Math.Clamp(step.DelayMilliseconds, 0, 10_000)}ms",
            _ => ""
        };
}

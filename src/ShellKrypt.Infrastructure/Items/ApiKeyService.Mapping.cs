using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ApiKeyService
{
    private static ApiKeyPayload ToPayload(ApiKeyInput input)
    {
        var fields = NormalizeFields(input.Fields).ToArray();
        if (fields.Length == 0)
            throw new InvalidOperationException("Add at least one API key field.");

        return new ApiKeyPayload(
            Name: NormalizeRequired(input.Name, "Name is required."),
            Provider: input.Provider.Trim(),
            Environment: string.IsNullOrWhiteSpace(input.Environment) ? DefaultEnvironment : input.Environment.Trim(),
            Notes: input.Notes.Trim(),
            Fields: fields,
            User: input.User.Trim());
    }

    private static IEnumerable<ApiKeyFieldPayload> NormalizeFields(IEnumerable<ApiKeyFieldInput>? fields)
    {
        var order = 0;
        foreach (var field in fields ?? Array.Empty<ApiKeyFieldInput>())
        {
            if (string.IsNullOrWhiteSpace(field.Label) && string.IsNullOrWhiteSpace(field.Value))
                continue;

            if (string.IsNullOrWhiteSpace(field.Label))
                throw new InvalidOperationException("Every API key field needs a label.");

            if (string.IsNullOrWhiteSpace(field.Value))
                throw new InvalidOperationException($"Field \"{field.Label.Trim()}\" needs a value.");

            yield return new ApiKeyFieldPayload(
                Id: string.IsNullOrWhiteSpace(field.Id) ? Guid.NewGuid().ToString("N") : field.Id.Trim(),
                Label: field.Label.Trim(),
                FieldType: string.IsNullOrWhiteSpace(field.FieldType) ? DefaultFieldType : field.FieldType.Trim(),
                Value: field.Value,
                IsSensitive: field.IsSensitive,
                IsCopyable: field.IsCopyable,
                SortOrder: field.SortOrder <= 0 ? order : field.SortOrder);
            order++;
        }
    }

    private static ApiKeyEntry ToEntry(VaultItemHeader header, ApiKeyPayload payload)
        => new(
            Id: header.Id,
            Name: payload.Name,
            Provider: payload.Provider,
            Environment: string.IsNullOrWhiteSpace(payload.Environment) ? DefaultEnvironment : payload.Environment,
            Notes: payload.Notes,
            Fields: payload.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field => new ApiKeyFieldEntry(
                    field.Id,
                    field.Label,
                    field.FieldType,
                    field.Value,
                    field.IsSensitive,
                    field.IsCopyable,
                    field.SortOrder))
                .ToArray(),
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc,
            User: payload.User ?? "");

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);

        return trimmed;
    }
}

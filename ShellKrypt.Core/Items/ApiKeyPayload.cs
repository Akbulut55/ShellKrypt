namespace ShellKrypt.Core.Items;

public sealed record ApiKeyPayload(
    string Name,
    string Provider,
    string Environment,
    string Notes,
    IReadOnlyList<ApiKeyFieldPayload> Fields,
    string User = "");

public sealed record ApiKeyFieldPayload(
    string Id,
    string Label,
    string FieldType,
    string Value,
    bool IsSensitive,
    bool IsCopyable,
    int SortOrder);

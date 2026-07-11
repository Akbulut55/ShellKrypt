namespace ShellKrypt.Core.Items;

public sealed record ApiKeyFieldInput(
    string Id,
    string Label,
    string FieldType,
    string Value,
    bool IsSensitive,
    bool IsCopyable,
    int SortOrder);

public sealed record ApiKeyInput(
    string Name,
    string Provider,
    string Environment,
    string Notes,
    IReadOnlyList<ApiKeyFieldInput> Fields,
    string User = "");

public sealed record ApiKeyFieldEntry(
    string Id,
    string Label,
    string FieldType,
    string Value,
    bool IsSensitive,
    bool IsCopyable,
    int SortOrder);

public sealed record ApiKeyEntry(
    string Id,
    string Name,
    string Provider,
    string Environment,
    string Notes,
    IReadOnlyList<ApiKeyFieldEntry> Fields,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    string User = "");

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<ApiKeyEntry> AddAsync(string vaultPath, byte[] vaultKey, ApiKeyInput input, CancellationToken ct = default);
    Task<ApiKeyEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, ApiKeyInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

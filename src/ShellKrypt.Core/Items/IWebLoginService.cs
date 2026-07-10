namespace ShellKrypt.Core.Items;

public sealed record WebLoginInput(
    string Title,
    string Url,
    string Username,
    string Email,
    string Password,
    string Notes);

public sealed record WebLoginEntry(
    string Id,
    string Title,
    string Url,
    string Username,
    string Email,
    string Password,
    string Notes,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public interface IWebLoginService
{
    Task<IReadOnlyList<WebLoginEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<WebLoginEntry> AddAsync(string vaultPath, byte[] vaultKey, WebLoginInput input, CancellationToken ct = default);
    Task<WebLoginEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, WebLoginInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

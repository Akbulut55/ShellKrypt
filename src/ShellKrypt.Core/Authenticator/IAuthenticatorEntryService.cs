namespace ShellKrypt.Core.Authenticator;

public interface IAuthenticatorEntryService
{
    Task<IReadOnlyList<AuthenticatorEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<AuthenticatorEntry> AddAsync(string vaultPath, byte[] vaultKey, AuthenticatorInput input, CancellationToken ct = default);
    Task<AuthenticatorEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, AuthenticatorInput input, CancellationToken ct = default);
    Task<AuthenticatorEntry> MarkUsedAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

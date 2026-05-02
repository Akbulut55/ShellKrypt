namespace ShellKrypt.Core.Items;

public sealed record AuthenticatorInput(
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter = 0,
    string Algorithm = "HMAC-SHA1",
    int Digits = 6,
    int PeriodSeconds = 30);

public sealed record AuthenticatorEntry(
    string Id,
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter,
    string Algorithm,
    int Digits,
    int PeriodSeconds,
    string LastUsedAtUtc,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public sealed record AuthenticatorCodeSnapshot(
    string Code,
    int SecondsRemaining,
    double ProgressPercent,
    bool IsValid);

public interface IAuthenticatorService
{
    Task<IReadOnlyList<AuthenticatorEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<AuthenticatorEntry> AddAsync(string vaultPath, byte[] vaultKey, AuthenticatorInput input, CancellationToken ct = default);
    Task<AuthenticatorEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, AuthenticatorInput input, CancellationToken ct = default);
    Task<AuthenticatorEntry> MarkUsedAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
    AuthenticatorCodeSnapshot GetCurrentCode(AuthenticatorEntry entry, DateTimeOffset? now = null);
}

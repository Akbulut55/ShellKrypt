namespace ShellKrypt.Core.Items;

public sealed record CardInput(
    string Title,
    string Bank,
    string Cardholder,
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvc,
    string Notes,
    string Issuer,
    string CardType);

public sealed record CardEntry(
    string Id,
    string Title,
    string Bank,
    string Cardholder,
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvc,
    string Notes,
    string Issuer,
    string CardType,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public interface ICardService
{
    Task<IReadOnlyList<CardEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<CardEntry> AddAsync(string vaultPath, byte[] vaultKey, CardInput input, CancellationToken ct = default);
    Task<CardEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, CardInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

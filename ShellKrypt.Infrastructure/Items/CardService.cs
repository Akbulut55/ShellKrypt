using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class CardService : ICardService
{
    private const int CardNumberMaxDigits = 16;
    private const int CvcMaxDigits = 4;
    private const string DefaultCardType = "Credit Card";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public CardService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<CardEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, ct);
        var cards = new List<CardEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Card))
        {
            var payload = DecryptPayload(vaultKey, row.EncryptedPayload);
            if (payload is null)
                continue;

            cards.Add(ToEntry(row.Header, payload));
        }

        return cards;
    }

    public async Task<CardEntry> AddAsync(string vaultPath, byte[] vaultKey, CardInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.Card,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<CardEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        CardInput input,
        CancellationToken ct = default)
    {
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.Card,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);

    private static byte[] EncryptPayload(byte[] vaultKey, CardPayload payload)
        => AesGcmBlob.Encrypt(vaultKey, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static CardPayload? DecryptPayload(byte[] vaultKey, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<CardPayload>(AesGcmBlob.Decrypt(vaultKey, encryptedPayload), JsonOpts);

    private static CardPayload ToPayload(CardInput input)
        => new(
            Title: input.Title.Trim(),
            Cardholder: input.Cardholder.Trim(),
            Number: DigitsOnly(input.Number, CardNumberMaxDigits),
            ExpiryMonth: input.ExpiryMonth,
            ExpiryYear: input.ExpiryYear,
            Cvc: DigitsOnly(input.Cvc, CvcMaxDigits),
            Notes: input.Notes.Trim(),
            Issuer: input.Issuer.Trim(),
            Bank: input.Bank.Trim(),
            CardType: string.IsNullOrWhiteSpace(input.CardType) ? DefaultCardType : input.CardType.Trim());

    private static CardEntry ToEntry(VaultItemHeader header, CardPayload payload)
        => new(
            Id: header.Id,
            Title: payload.Title,
            Bank: payload.Bank is null ? payload.Cardholder : payload.Bank,
            Cardholder: payload.Cardholder,
            Number: payload.Number,
            ExpiryMonth: payload.ExpiryMonth,
            ExpiryYear: payload.ExpiryYear,
            Cvc: payload.Cvc,
            Notes: payload.Notes,
            Issuer: payload.Issuer,
            CardType: string.IsNullOrWhiteSpace(payload.CardType) ? DefaultCardType : payload.CardType,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);

    private static string DigitsOnly(string? value, int maxDigits)
        => new((value ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());
}

using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class CardService
{
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

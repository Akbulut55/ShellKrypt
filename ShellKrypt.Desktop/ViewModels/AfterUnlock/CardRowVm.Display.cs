using System;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm
{
    public string IconLetter => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();

    public string NumberDisplay
        => IsSecretsVisible ? FormatCardNumber(Number) : MaskCardNumber(Number);

    public string ExpiryDisplay
        => $"{(string.IsNullOrWhiteSpace(ExpiryMonth) ? "MM" : ExpiryMonth)} / {FormatExpiryYear(ExpiryYear)}";

    public string SubtitleDisplay => string.IsNullOrWhiteSpace(Notes)
        ? (string.IsNullOrWhiteSpace(Cardholder) ? "Encrypted card" : Cardholder.Trim())
        : Notes.Trim();

    public string BankDisplay => string.IsNullOrWhiteSpace(Bank) ? "Unassigned" : Bank.Trim();

    public string IssuerDisplay => string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();

    public bool IsExpired => TryGetExpiryDate(out var expiry) && expiry < DateTime.Today;

    public bool IsExpiryUrgent => TryGetExpiryDate(out var expiry) &&
                                  expiry >= DateTime.Today &&
                                  expiry <= DateTime.Today.AddMonths(3);

    public string SecretsActionLabel => IsSecretsVisible ? "Hide" : "View";

    public void MarkSaved(string updatedAtUtc)
    {
        Issuer = string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
        UpdatedAtUtc = string.IsNullOrWhiteSpace(updatedAtUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : updatedAtUtc;
    }

    private bool TryGetExpiryDate(out DateTime expiry)
    {
        expiry = DateTime.MaxValue;
        if (!int.TryParse(ExpiryMonth, out var month) || month is < 1 or > 12)
            return false;
        if (!int.TryParse(ExpiryYear, out var year))
            return false;
        if (year < 100)
            year += 2000;

        expiry = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return true;
    }
}

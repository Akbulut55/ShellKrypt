using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm : ObservableObject
{
    internal const string DefaultCardType = "Credit Card";
    internal const int StandardCardNumberMaxDigits = 16;
    internal const int ExpiryMonthMaxDigits = 2;
    internal const int ExpiryYearMaxDigits = 4;
    internal const int CvcMaxDigits = 4;

    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string bank;
    [ObservableProperty] private string cardholder;
    [ObservableProperty] private string number;
    [ObservableProperty] private string expiryMonth;
    [ObservableProperty] private string expiryYear;
    [ObservableProperty] private string cvc;
    [ObservableProperty] private string notes;
    [ObservableProperty] private string issuer;
    [ObservableProperty] private string cardType;

    [ObservableProperty] private bool isSecretsVisible;

    public CardRowVm(
        string id,
        string title,
        string bank,
        string cardholder,
        string number,
        string expiryMonth,
        string expiryYear,
        string cvc,
        string notes,
        string issuer,
        string cardType,
        string createdAtUtc,
        string updatedAtUtc)
    {
        Id = id;
        Title = title ?? "";
        Bank = bank ?? "";
        Cardholder = cardholder ?? "";
        Number = number ?? "";
        ExpiryMonth = expiryMonth ?? "";
        ExpiryYear = expiryYear ?? "";
        Cvc = cvc ?? "";
        Notes = notes ?? "";
        Issuer = string.IsNullOrWhiteSpace(issuer) ? DetectIssuer(number) : issuer;
        CardType = string.IsNullOrWhiteSpace(cardType) ? DefaultCardType : cardType;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

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

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnBankChanged(string value) => OnPropertyChanged(nameof(BankDisplay));
    partial void OnNumberChanged(string value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        if (string.IsNullOrWhiteSpace(Issuer))
            OnPropertyChanged(nameof(IssuerDisplay));
    }
    partial void OnCvcChanged(string value)
    {
        var normalized = DigitsOnly(value, CvcMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            Cvc = normalized;
        }
    }
    partial void OnExpiryMonthChanged(string value)
    {
        var normalized = DigitsOnly(value, ExpiryMonthMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            ExpiryMonth = normalized;
            return;
        }

        NotifyExpiryChanged();
    }
    partial void OnExpiryYearChanged(string value)
    {
        var normalized = DigitsOnly(value, ExpiryYearMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            ExpiryYear = normalized;
            return;
        }

        NotifyExpiryChanged();
    }
    partial void OnCardholderChanged(string value) => OnPropertyChanged(nameof(SubtitleDisplay));
    partial void OnNotesChanged(string value)
    {
        OnPropertyChanged(nameof(SubtitleDisplay));
    }
    partial void OnIssuerChanged(string value) => OnPropertyChanged(nameof(IssuerDisplay));
    partial void OnIsSecretsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        OnPropertyChanged(nameof(SecretsActionLabel));
    }

    public void MarkSaved(string updatedAtUtc)
    {
        Issuer = string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
        UpdatedAtUtc = string.IsNullOrWhiteSpace(updatedAtUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : updatedAtUtc;
    }

    internal static string FormatCardNumber(string? number, int maxDigits = 19, bool includeTrailingSeparator = false)
    {
        var digits = DigitsOnly(number, maxDigits);
        if (digits.Length == 0)
            return "";

        var groups = new List<string>();
        for (var i = 0; i < digits.Length; i += 4)
        {
            var length = Math.Min(4, digits.Length - i);
            groups.Add(digits.Substring(i, length));
        }

        var formatted = string.Join(" ", groups);
        if (includeTrailingSeparator && digits.Length % 4 == 0 && digits.Length < maxDigits)
            return formatted + " ";

        return formatted;
    }

    internal static string DigitsOnly(string? value, int maxDigits)
        => new((value ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());

    internal static string DetectIssuer(string? number)
    {
        var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("4", StringComparison.Ordinal))
            return "Visa";
        if (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal))
            return "Amex";
        if (digits.Length >= 2 && int.TryParse(digits[..2], out var prefix2))
        {
            if (prefix2 is >= 51 and <= 55)
                return "Mastercard";
            if (prefix2 is 36 or 38 or 39)
                return "Diners Club";
            if (prefix2 == 62)
                return "UnionPay";
            if (prefix2 == 65)
                return "Discover";
        }
        if (digits.Length >= 3 && int.TryParse(digits[..3], out var prefix3))
        {
            if (prefix3 is >= 300 and <= 305)
                return "Diners Club";
            if (prefix3 is >= 644 and <= 649)
                return "Discover";
        }
        if (digits.StartsWith("35", StringComparison.Ordinal))
            return "JCB";
        if (digits.StartsWith("6011", StringComparison.Ordinal))
            return "Discover";
        if (digits.Length >= 4 && int.TryParse(digits[..4], out var prefix4) && prefix4 is >= 2221 and <= 2720)
            return "Mastercard";

        return "Card";
    }

    private static string MaskCardNumber(string? n)
    {
        if (string.IsNullOrWhiteSpace(n))
            return "";

        var digits = new string(n.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        var last4 = digits[^4..];
        return $"**** **** **** {last4}";
    }

    private void NotifyExpiryChanged()
    {
        OnPropertyChanged(nameof(ExpiryDisplay));
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(IsExpiryUrgent));
    }

    private static string FormatExpiryYear(string year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return "YY";

        var trimmed = year.Trim();
        return trimmed.Length >= 2 ? trimmed[^2..] : trimmed;
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

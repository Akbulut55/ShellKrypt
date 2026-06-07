using System;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm
{
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

    private void NotifyExpiryChanged()
    {
        OnPropertyChanged(nameof(ExpiryDisplay));
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(IsExpiryUrgent));
    }
}

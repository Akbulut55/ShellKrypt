using ShellKrypt.Core.Items;
using System;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    private CardInput BuildInput(string digits, int expiryMonth, int expiryYear, string cvcDigits)
        => new(
            Title: AddTitle,
            Bank: AddBank,
            Cardholder: AddCardholder,
            Number: digits,
            ExpiryMonth: expiryMonth,
            ExpiryYear: expiryYear,
            Cvc: cvcDigits,
            Notes: AddNotes,
            Issuer: string.IsNullOrWhiteSpace(AddIssuer) ? CardRowVm.DetectIssuer(digits) : AddIssuer,
            CardType: string.IsNullOrWhiteSpace(AddCardType) ? CardRowVm.DefaultCardType : AddCardType);

    private static CardRowVm ToRow(CardEntry entry)
        => new(
            entry.Id,
            entry.Title,
            entry.Bank,
            entry.Cardholder,
            entry.Number,
            entry.ExpiryMonth.ToString("00"),
            entry.ExpiryYear.ToString(),
            entry.Cvc,
            entry.Notes,
            entry.Issuer,
            string.IsNullOrWhiteSpace(entry.CardType) ? CardRowVm.DefaultCardType : entry.CardType,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc);

    private static void ApplyEntry(CardRowVm row, CardEntry entry)
    {
        row.Title = entry.Title;
        row.Bank = entry.Bank;
        row.Cardholder = entry.Cardholder;
        row.Number = entry.Number;
        row.ExpiryMonth = entry.ExpiryMonth.ToString("00");
        row.ExpiryYear = entry.ExpiryYear.ToString();
        row.Cvc = entry.Cvc;
        row.Notes = entry.Notes;
        row.Issuer = entry.Issuer;
        row.CardType = string.IsNullOrWhiteSpace(entry.CardType) ? CardRowVm.DefaultCardType : entry.CardType;
        row.MarkSaved(entry.UpdatedAtUtc);
    }

    private void UpdateAddIssuerFromNumber(string number)
    {
        var detected = CardRowVm.DetectIssuer(number);
        if (string.Equals(detected, DefaultIssuer, StringComparison.Ordinal))
        {
            if (string.Equals(AddIssuer, _lastAutoAddIssuer, StringComparison.Ordinal))
            {
                AddIssuer = DefaultIssuer;
                _lastAutoAddIssuer = DefaultIssuer;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(AddIssuer) ||
            string.Equals(AddIssuer, DefaultIssuer, StringComparison.Ordinal) ||
            string.Equals(AddIssuer, _lastAutoAddIssuer, StringComparison.Ordinal))
        {
            AddIssuer = detected;
            _lastAutoAddIssuer = detected;
        }
    }
}

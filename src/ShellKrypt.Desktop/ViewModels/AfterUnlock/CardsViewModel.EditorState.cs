namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    private void ClearAddCardForm()
    {
        AddTitle = "";
        AddBank = "";
        AddCardholder = "";
        AddIssuer = DefaultIssuer;
        AddCardType = CardRowVm.DefaultCardType;
        _lastAutoAddIssuer = DefaultIssuer;
        AddNumber = "";
        AddExpiryMonth = "";
        AddExpiryYear = "";
        AddCvc = "";
        IsAddCvcVisible = false;
        AddNotes = "";
    }

    private void PopulateModalFromRow(CardRowVm row)
    {
        AddTitle = row.Title;
        AddBank = row.Bank;
        AddCardholder = row.Cardholder;
        AddNumber = row.Number;
        AddExpiryMonth = row.ExpiryMonth;
        AddExpiryYear = row.ExpiryYear;
        AddCvc = row.Cvc;
        IsAddCvcVisible = false;
        AddNotes = row.Notes;
        AddCardType = string.IsNullOrWhiteSpace(row.CardType) ? CardRowVm.DefaultCardType : row.CardType;
        AddIssuer = string.IsNullOrWhiteSpace(row.Issuer) ? row.IssuerDisplay : row.Issuer;
        _lastAutoAddIssuer = AddIssuer;
    }
}

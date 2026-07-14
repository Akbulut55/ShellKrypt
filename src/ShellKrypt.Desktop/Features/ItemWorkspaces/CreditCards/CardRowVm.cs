using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;

public sealed partial class CardRowVm : ObservableObject
{
    internal const string DefaultCardType = "Credit Card";
    internal const int StandardCardNumberMaxDigits = 16;
    internal const int ExpiryMonthMaxDigits = 2;
    internal const int ExpiryYearMaxDigits = 4;
    internal const int CvcMaxDigits = 4;

    private readonly LocalizationService _localization;

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
        LocalizationService localization,
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
        _localization = localization;
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

    internal string T(string key, params object[] args) => _localization.Get(key, args);
}

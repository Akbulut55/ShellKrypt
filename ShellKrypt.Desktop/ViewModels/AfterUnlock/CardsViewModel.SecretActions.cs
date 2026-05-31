using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    [RelayCommand]
    private void ToggleSecrets(CardRowVm row)
        => row.IsSecretsVisible = !row.IsSecretsVisible;

    [RelayCommand]
    private void ToggleAddCvcVisibility()
        => IsAddCvcVisible = !IsAddCvcVisible;

    [RelayCommand]
    private async Task CopyCardNumberAsync(CardRowVm row)
    {
        Error = "";

        var digits = CardRowVm.DigitsOnly(row.Number, CardRowVm.StandardCardNumberMaxDigits);
        if (string.IsNullOrWhiteSpace(digits))
        {
            Error = "No card number to copy.";
            return;
        }

        await _root.CopyToClipboardAsync(digits);
        _root.LogActivity("cards", "Card number copied", $"Copied card number for {row.Title}.", "info", affectedItem: row.Title);
    }
}

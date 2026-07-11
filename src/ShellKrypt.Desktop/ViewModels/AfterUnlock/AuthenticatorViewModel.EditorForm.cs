using System.Globalization;
using System.Linq;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    private void PopulateEditorForm(AuthenticatorAccountVm entry)
    {
        FormName = entry.Name;
        FormSecret = entry.Secret;
        SelectedFormKeyType = KeyTypeOptions.First(option => option.KeyType == entry.KeyType);
        _formCounter = entry.Counter;
        SelectedFormAlgorithm = ResolveAlgorithmOption(entry.Algorithm);
        SelectedFormDigits = ResolveDigitsOption(entry.Digits);
        FormPeriodSecondsText = entry.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }

    private void ClearEditorForm()
    {
        FormName = string.Empty;
        FormSecret = string.Empty;
        SelectedFormKeyType = KeyTypeOptions[0];
        SelectedFormAlgorithm = AlgorithmOptions[0];
        SelectedFormDigits = DigitsOptions[0];
        FormPeriodSecondsText = "30";
        _formCounter = 0;
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }

    private void ApplyImportedSecret(ParsedOtpAuthSecret parsed)
    {
        FormName = parsed.Name;
        FormSecret = parsed.Secret;
        SelectedFormKeyType = KeyTypeOptions.First(option => option.KeyType == parsed.KeyType);
        SelectedFormAlgorithm = ResolveAlgorithmOption(parsed.Algorithm);
        SelectedFormDigits = ResolveDigitsOption(parsed.Digits);
        FormPeriodSecondsText = parsed.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        _formCounter = parsed.Counter;
        IsEditorModalOpen = true;
        IsFormSecretVisible = false;
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }
}

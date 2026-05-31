using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private void AddNew()
    {
        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsEditingExisting = false;
        ClearEditorForm();
        IsFormSecretVisible = false;
        IsAdvancedOptionsExpanded = false;
        IsEditorModalOpen = true;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsEditingExisting = true;
        PopulateEditorForm(SelectedEntry);
        IsFormSecretVisible = false;
        IsAdvancedOptionsExpanded = false;
        IsEditorModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        BeginEdit();
    }

    [RelayCommand]
    private void CancelEditor()
    {
        Error = string.Empty;
        IsEditorModalOpen = false;
        IsFormSecretVisible = false;
    }

    [RelayCommand]
    private void ToggleSecretVisibility(AuthenticatorAccountVm? entry)
    {
        if (entry is not null)
            entry.IsSecretVisible = !entry.IsSecretVisible;
    }

    [RelayCommand]
    private void ToggleFormSecretVisibility()
    {
        IsFormSecretVisible = !IsFormSecretVisible;
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormName))
        {
            Error = "Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormSecret))
        {
            Error = "Secret key is required.";
            return;
        }

        if (SelectedFormKeyType is null)
        {
            Error = "Select a key type.";
            return;
        }

        IsBusy = true;
        try
        {
            var input = new AuthenticatorInput(
                Name: FormName,
                Secret: FormSecret,
                KeyType: SelectedFormKeyType.KeyType,
                Counter: SelectedFormKeyType.KeyType == AuthenticatorKeyType.CounterBased ? _formCounter : 0,
                Algorithm: SelectedFormAlgorithm?.Value ?? "HMAC-SHA1",
                Digits: SelectedFormDigits?.Digits ?? 6,
                PeriodSeconds: ResolveFormPeriodSeconds());

            if (IsEditingExisting)
            {
                if (SelectedEntry is null)
                {
                    Error = "No authenticator code selected.";
                    return;
                }

                var updated = await _authenticatorService.UpdateAsync(
                    _root.VaultPath,
                    _root.VaultKey,
                    SelectedEntry.Id,
                    SelectedEntry.CreatedAtUtc,
                    input);

                SelectedEntry.Apply(updated);
                RefreshSnapshots();
                await _refreshAllItemsAsync(updated.Id);
                _root.LogActivity("authenticator", "Authenticator updated", $"Updated {updated.Name}.", "info", affectedItem: updated.Name);
            }
            else
            {
                var added = await _authenticatorService.AddAsync(_root.VaultPath, _root.VaultKey, input);
                var vm = new AuthenticatorAccountVm(added);
                _allEntries.Insert(0, vm);
                RefreshSnapshots();
                ApplyFilter(selectEntryId: added.Id);
                await _refreshAllItemsAsync(added.Id);
                _root.LogActivity("authenticator", "Authenticator added", $"Added {added.Name}.", "success", affectedItem: added.Name);
            }

            IsEditorModalOpen = false;
            IsDetailsModalOpen = false;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

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

    private AuthenticatorAlgorithmOption ResolveAlgorithmOption(string? value)
    {
        var normalized = NormalizeAlgorithm(value);
        return AlgorithmOptions.First(option => string.Equals(option.Value, normalized, StringComparison.Ordinal));
    }

    private AuthenticatorDigitsOption ResolveDigitsOption(int digits)
    {
        var normalized = digits == 8 ? 8 : 6;
        return DigitsOptions.First(option => option.Digits == normalized);
    }

    private int ResolveFormPeriodSeconds()
    {
        if (SelectedFormKeyType?.KeyType == AuthenticatorKeyType.CounterBased)
            return 30;

        if (!int.TryParse(FormPeriodSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 1 ||
            seconds > 300)
        {
            throw new InvalidOperationException("Period must be a whole number between 1 and 300 seconds.");
        }

        return seconds;
    }

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => "HMAC-SHA1"
        };
    }

    private static string NormalizePeriodText(string? value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : "30";
    }
}

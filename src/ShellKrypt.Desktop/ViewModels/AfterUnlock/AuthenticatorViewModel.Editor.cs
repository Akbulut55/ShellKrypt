using CommunityToolkit.Mvvm.Input;

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
}

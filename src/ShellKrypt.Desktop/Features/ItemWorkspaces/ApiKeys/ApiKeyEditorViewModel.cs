using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.UI.Shared.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;

public partial class ApiKeyEditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IApiKeyService _service;
    private readonly Func<ApiKeyEntry?, string?, Task> _onMutation;
    private readonly Func<string?, Task> _refreshAllItems;
    private ApiKeyRowVm? _selected;
    private string _fieldId = "";
    private string _environment = "Production";

    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private ItemEditorMode mode;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string provider = "";
    [ObservableProperty] private string user = "";
    [ObservableProperty] private string value = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private bool valueVisible;
    [ObservableProperty] private string error = "";

    public ApiKeyEditorViewModel(
        MainWindowViewModel root,
        IApiKeyService service,
        Func<ApiKeyEntry?, string?, Task> onMutation,
        Func<string?, Task> refreshAllItems)
    {
        _root = root;
        _service = service;
        _onMutation = onMutation;
        _refreshAllItems = refreshAllItems;
    }

    public bool IsAdd => Mode == ItemEditorMode.Add;
    public bool IsDetails => Mode == ItemEditorMode.Details;
    public bool IsEdit => Mode == ItemEditorMode.Edit;
    public bool IsConfirmDelete => Mode == ItemEditorMode.ConfirmDelete;
    public bool IsEditable => IsAdd || IsEdit;
    public ModalShellSize ModalSize => IsDetails ? ModalShellSize.ItemDetails : ModalShellSize.Standard;
    public string ValueDisplay => ValueVisible ? Value : Value.Length == 0 ? "" : "**** **** " + (Value.Length > 4 ? Value[^4..] : "");
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? T(_root, "ItemWorkspace.Details.NoNotes") : Notes.Trim();
    public string CredentialBadge => T(_root, "ItemWorkspace.Details.ApiCredential");
    public string ModalTitle => IsAdd ? T(_root, "ApiKeys.Modal.AddTitle") : IsEdit ? T(_root, "ApiKeys.Modal.EditTitle") : IsConfirmDelete ? T(_root, "ApiKeys.Modal.DeleteTitle") : T(_root, "ApiKeys.Modal.DetailsTitle");
    public string ModalSubtitle => IsAdd ? T(_root, "ApiKeys.Modal.AddSubtitle") : IsEdit ? T(_root, "ApiKeys.Modal.EditSubtitle") : IsConfirmDelete ? T(_root, "ApiKeys.Modal.DeleteSubtitle") : T(_root, "ItemWorkspace.Details.StoredLocally");
    public string FooterText => IsDetails ? "" : IsConfirmDelete ? T(_root, "ApiKeys.Modal.DeleteFooter", Name) : T(_root, "ApiKeys.Modal.Footer");

    partial void OnModeChanged(ItemEditorMode value)
    {
        if (value != ItemEditorMode.Details)
            ValueVisible = false;
        NotifyMode();
    }

    partial void OnValueVisibleChanged(bool value) => OnPropertyChanged(nameof(ValueDisplay));
    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(ValueDisplay));
    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(NotesDisplay));

    public void OpenAdd()
    {
        _selected = null;
        Clear();
        Mode = ItemEditorMode.Add;
        IsOpen = true;
    }

    public void OpenDetails(ApiKeyRowVm row)
    {
        _selected = row;
        Populate(row);
        Mode = ItemEditorMode.Details;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        _selected = null;
        Clear();
    }

    [RelayCommand] private void BeginEdit() => Mode = ItemEditorMode.Edit;
    [RelayCommand] private void BeginDelete() => Mode = ItemEditorMode.ConfirmDelete;
    [RelayCommand] private void CancelDelete() => Mode = ItemEditorMode.Details;
    [RelayCommand] private void CancelEdit() { if (IsAdd) Close(); else { if (_selected is not null) Populate(_selected); Mode = ItemEditorMode.Details; } }
    [RelayCommand] private void Cancel() { if (IsConfirmDelete) CancelDelete(); else CancelEdit(); }
    [RelayCommand] private void ToggleValue() => ValueVisible = !ValueVisible;
    [RelayCommand] private async Task CopyValueAsync() { if (Value.Length > 0) await _root.CopyToClipboardAsync(Value); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";
        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }
        if (string.IsNullOrWhiteSpace(Name)) { Error = T(_root, "Validation.NameRequired"); return; }

        try
        {
            var primaryField = new ApiKeyFieldInput(
                string.IsNullOrWhiteSpace(_fieldId) ? Guid.NewGuid().ToString("N") : _fieldId,
                "API Key",
                ApiKeysViewModel.DefaultFieldType,
                Value,
                true,
                true,
                0);
            IReadOnlyList<ApiKeyFieldInput> fields = _selected is null || _selected.Fields.Count == 0
                ? [primaryField]
                : _selected.Fields.Select(existing => existing.Id == primaryField.Id
                    ? primaryField
                    : new ApiKeyFieldInput(existing.Id, existing.Label, existing.FieldType, existing.Value, existing.IsSensitive, existing.IsCopyable, existing.SortOrder)).ToArray();
            var input = new ApiKeyInput(Name, Provider, _environment, Notes, fields, User);
            var entry = _selected is null
                ? await _service.AddAsync(_root.VaultPath, _root.VaultKey, input)
                : await _service.UpdateAsync(_root.VaultPath, _root.VaultKey, _selected.Id, _selected.CreatedAtUtc, input);

            await _refreshAllItems(entry.Id);
            await _onMutation(entry, null);
            _root.LogActivity("api_keys", _selected is null ? "API key added" : "API key updated", $"{(_selected is null ? "Added" : "Updated")} {entry.Name}.", _selected is null ? "success" : "info", affectedItem: entry.Name);
            _selected = new ApiKeyRowVm(entry, _root.Localization);
            Populate(_selected);
            Mode = ItemEditorMode.Details;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_selected is null || _root.VaultPath is null)
            return;
        try
        {
            var row = _selected;
            await _service.DeleteAsync(_root.VaultPath, row.Id);
            await _refreshAllItems(null);
            await _onMutation(null, row.Id);
            _root.LogActivity("api_keys", "API key deleted", $"Deleted {row.Name}.", "warning", affectedItem: row.Name);
            Close();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void Populate(ApiKeyRowVm row)
    {
        Name = row.Name;
        Provider = row.Provider;
        User = row.User;
        Value = row.PrimaryCopyValue;
        Notes = row.Notes;
        _fieldId = row.PrimaryField?.Id ?? "";
        _environment = string.IsNullOrWhiteSpace(row.Environment) ? "Production" : row.Environment;
        ValueVisible = false;
        Error = "";
    }

    private void Clear()
    {
        Name = Provider = User = Value = Notes = Error = "";
        _fieldId = "";
        _environment = "Production";
        ValueVisible = false;
    }

    private void NotifyMode()
    {
        OnPropertyChanged(nameof(IsAdd));
        OnPropertyChanged(nameof(IsDetails));
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(IsConfirmDelete));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(ModalSize));
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(ModalSubtitle));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(NotesDisplay));
        OnPropertyChanged(nameof(CredentialBadge));
    }

    public override void RefreshLocalization() => NotifyMode();
}

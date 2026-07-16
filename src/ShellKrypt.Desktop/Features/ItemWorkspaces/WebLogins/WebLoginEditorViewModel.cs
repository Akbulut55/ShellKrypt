using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Services.Runtime;
using ShellKrypt.UI.Shared.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;

public partial class WebLoginEditorViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _desktop;
    private readonly IWebLoginService _service;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly Func<WebLoginEntry?, string?, Task> _onMutation;
    private readonly Func<string?, Task> _refreshAllItems;
    private WebLoginRowVm? _selected;

    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private ItemEditorMode mode;
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string url = "";
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private bool isPasswordVisible;
    [ObservableProperty] private string error = "";

    public WebLoginEditorViewModel(DesktopFeatureServices desktop, IWebLoginService service, IPasswordGenerator passwordGenerator, Func<WebLoginEntry?, string?, Task> onMutation, Func<string?, Task> refreshAllItems)
    { _desktop = desktop; _service = service; _passwordGenerator = passwordGenerator; _onMutation = onMutation; _refreshAllItems = refreshAllItems; }

    public bool IsAdd => Mode == ItemEditorMode.Add;
    public bool IsDetails => Mode == ItemEditorMode.Details;
    public bool IsEdit => Mode == ItemEditorMode.Edit;
    public bool IsConfirmDelete => Mode == ItemEditorMode.ConfirmDelete;
    public bool IsEditable => IsAdd || IsEdit;
    public ModalShellSize ModalSize => IsDetails ? ModalShellSize.ItemDetails : ModalShellSize.Standard;
    public string UrlHostDisplay => FormatUrlHost(Url);
    public string PasswordDisplay => IsPasswordVisible ? Password : string.IsNullOrEmpty(Password) ? "" : "••••••••••";
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? T(_desktop.Localization, "ItemWorkspace.Details.NoNotes") : Notes.Trim();
    public string ModalTitle => IsAdd ? T(_desktop.Localization, "WebLogins.Modal.AddTitle") : IsEdit ? T(_desktop.Localization, "WebLogins.Modal.EditTitle") : IsConfirmDelete ? T(_desktop.Localization, "WebLogins.Modal.DeleteTitle") : T(_desktop.Localization, "WebLogins.Modal.DetailsTitle");
    public string ModalSubtitle => IsAdd ? T(_desktop.Localization, "WebLogins.Modal.AddSubtitle") : IsEdit ? T(_desktop.Localization, "WebLogins.Modal.EditSubtitle") : IsConfirmDelete ? T(_desktop.Localization, "WebLogins.Modal.DeleteSubtitle") : T(_desktop.Localization, "ItemWorkspace.Details.StoredLocally");
    public string FooterText => IsDetails ? "" : IsConfirmDelete ? T(_desktop.Localization, "WebLogins.Modal.DeleteFooter", Title) : T(_desktop.Localization, "WebLogins.Modal.Footer");

    partial void OnModeChanged(ItemEditorMode value) { if (value != ItemEditorMode.Details) IsPasswordVisible = false; NotifyMode(); }
    partial void OnUrlChanged(string value) => OnPropertyChanged(nameof(UrlHostDisplay));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(NotesDisplay));

    public void OpenAdd() { _selected = null; Clear(); Mode = ItemEditorMode.Add; IsOpen = true; }
    public void OpenDetails(WebLoginRowVm row, bool editImmediately = false, bool generateReplacementPassword = false)
    {
        _selected = row; Populate(row); Mode = editImmediately ? ItemEditorMode.Edit : ItemEditorMode.Details;
        if (generateReplacementPassword) GeneratePassword();
        IsOpen = true;
    }

    [RelayCommand] private void Close() { IsOpen = false; _selected = null; Clear(); Mode = ItemEditorMode.Add; }
    [RelayCommand] private void BeginEdit() { if (_selected is not null) Mode = ItemEditorMode.Edit; }
    [RelayCommand] private void BeginDelete() { if (_selected is not null) Mode = ItemEditorMode.ConfirmDelete; }
    [RelayCommand] private void CancelDelete() => Mode = ItemEditorMode.Details;
    [RelayCommand] private void CancelEdit() { if (Mode == ItemEditorMode.Add) Close(); else { if (_selected is not null) Populate(_selected); Mode = ItemEditorMode.Details; } }
    [RelayCommand] private void Cancel() { if (IsConfirmDelete) CancelDelete(); else CancelEdit(); }
    [RelayCommand] private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;
    [RelayCommand] private void GeneratePassword() { Password = _passwordGenerator.GeneratePassword(new(32, true, true, true, true)) ?? ""; IsPasswordVisible = true; Error = ""; }
    [RelayCommand] private async Task CopyPasswordAsync() { if (Password.Length == 0) { Error = T(_desktop.Localization, "WebLogins.Error.NoPassword"); return; } await _desktop.Clipboard.CopyAsync(Password); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";
        if (_desktop.Session.VaultPath is null) { Error = T(_desktop.Localization, "Common.NoVaultSelected"); return; }
        if (string.IsNullOrWhiteSpace(Title)) { Error = T(_desktop.Localization, "Validation.TitleRequired"); return; }
        try
        {
            var input = new WebLoginInput(Title, Url, Username, Email, Password, Notes);
            var entry = _selected is null
                ? await _service.AddAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey, input)
                : await _service.UpdateAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey, _selected.Id, _selected.CreatedAtUtc, input);
            await _refreshAllItems(entry.Id); await _onMutation(entry, null);
            _desktop.Activity.Log("web", _selected is null ? "Web login added" : "Web login updated", $"{(_selected is null ? "Added" : "Updated")} {entry.Title}.", _selected is null ? "success" : "info", affectedItem: entry.Title);
            _selected = new WebLoginRowVm(_desktop.Localization, entry.Id, entry.Title, entry.Username, entry.Password, entry.Url, entry.Notes, entry.CreatedAtUtc, entry.UpdatedAtUtc, false, entry.Email);
            Populate(_selected); Mode = ItemEditorMode.Details;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_selected is null || _desktop.Session.VaultPath is null) return;
        try
        {
            var row = _selected; await _service.DeleteAsync(_desktop.Session.VaultPath, row.Id); await _refreshAllItems(null); await _onMutation(null, row.Id);
            _desktop.Activity.Log("web", "Web login deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title); Close();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void Populate(WebLoginRowVm row) { Title = row.Title; Url = row.Url; Username = row.Username; Email = row.Email; Password = row.Password; Notes = row.Notes; IsPasswordVisible = false; Error = ""; }
    private void Clear() { Title = Url = Username = Email = Password = Notes = Error = ""; IsPasswordVisible = false; }
    private string FormatUrlHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return T(_desktop.Localization, "WebLogins.Row.NoUrl");
        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host;
        var withoutScheme = text.Replace("https://", "", StringComparison.OrdinalIgnoreCase).Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        var slash = withoutScheme.IndexOf('/');
        return slash < 0 ? withoutScheme : withoutScheme[..slash];
    }

    private void NotifyMode() { OnPropertyChanged(nameof(IsAdd)); OnPropertyChanged(nameof(IsDetails)); OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(IsConfirmDelete)); OnPropertyChanged(nameof(IsEditable)); OnPropertyChanged(nameof(ModalSize)); OnPropertyChanged(nameof(ModalTitle)); OnPropertyChanged(nameof(ModalSubtitle)); OnPropertyChanged(nameof(FooterText)); OnPropertyChanged(nameof(NotesDisplay)); OnPropertyChanged(nameof(UrlHostDisplay)); OnPropertyChanged(nameof(PasswordDisplay)); }
    public override void RefreshLocalization() => NotifyMode();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WebLoginRowVm : ObservableObject
{
    public string Id { get; }
    public bool IsNew { get; private set; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string username;
    [ObservableProperty] private string password;

    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isPasswordVisible;

    // for cancel revert
    private string _origTitle = "";
    private string _origUsername = "";
    private string _origPassword = "";

    public WebLoginRowVm(string id, string title, string username, string password, string createdAtUtc, string updatedAtUtc, bool isNew)
    {
        Id = id;
        Title = title;
        Username = username;
        Password = password;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsNew = isNew;
    }

    public string IconLetter => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[0].ToString().ToUpperInvariant();
    public string PasswordDisplay => IsPasswordVisible ? Password : "••••••••••";

    public bool IsViewing => !IsEditing;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsViewing));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(PasswordDisplay));

    public void BeginEdit()
    {
        _origTitle = Title;
        _origUsername = Username;
        _origPassword = Password;
        IsEditing = true;
    }

    public void CancelEdit(bool removeIfNew, Action<WebLoginRowVm> removeRow)
    {
        if (removeIfNew && IsNew)
        {
            removeRow(this);
            return;
        }

        Title = _origTitle;
        Username = _origUsername;
        Password = _origPassword;
        IsEditing = false;
    }

    public void MarkSaved()
    {
        IsNew = false;
        IsEditing = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
    }
}

public partial class WebLoginsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    private readonly List<WebLoginRowVm> _all = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<WebLoginRowVm> Rows { get; } = new();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string error = "";

    public WebLoginsViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void AddNew()
    {
        Error = "";

        var now = DateTimeOffset.UtcNow.ToString("O");
        var row = new WebLoginRowVm(
            id: Guid.NewGuid().ToString("N"),
            title: "",
            username: "",
            password: "",
            createdAtUtc: now,
            updatedAtUtc: now,
            isNew: true
        );

        row.IsEditing = true;

        _all.Insert(0, row);
        Rows.Insert(0, row);
    }

    [RelayCommand]
    private void BeginEdit(WebLoginRowVm row)
    {
        Error = "";
        row.BeginEdit();
        
    }

    [RelayCommand]
    private async Task SaveAsync(WebLoginRowVm row)
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(row.Title)) { Error = "Title is required."; return; }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var payload = new WebPayload(
                Title: row.Title,
                Url: "",
                Username: row.Username,
                Password: row.Password,
                Notes: "",
                TwoFaNote: ""
            );

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);

            var header = new VaultItemHeader(
                Id: row.Id,
                Type: ItemType.Web,
                Favorite: false,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: now
            );

            if (row.IsNew)
                await _repo.InsertAsync(_root.VaultPath, header, enc);
            else
                await _repo.UpdateAsync(_root.VaultPath, header, enc);

            row.MarkSaved();
            
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel(WebLoginRowVm row)
    {
        Error = "";
        row.CancelEdit(removeIfNew: true, removeRow: RemoveRow);
        
    }

    [RelayCommand]
    private async Task DeleteAsync(WebLoginRowVm row)
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            await _repo.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void TogglePassword(WebLoginRowVm row)
    {
        row.IsPasswordVisible = !row.IsPasswordVisible;
        
    }

    private void RemoveRow(WebLoginRowVm row)
    {
        _all.Remove(row);
        Rows.Remove(row);
    }

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            _all.Clear();
            Rows.Clear();

            var rows = await _repo.ListAsync(_root.VaultPath);

            foreach (var r in rows.Where(x => x.Header.Type == ItemType.Web))
            {
                var json = AesGcmBlob.Decrypt(_root.VaultKey, r.EncryptedPayload);
                var payload = JsonSerializer.Deserialize<WebPayload>(json, JsonOpts);
                if (payload is null) continue;

                _all.Add(new WebLoginRowVm(
                    r.Header.Id,
                    payload.Title,
                    payload.Username,
                    payload.Password,
                    r.Header.CreatedAtUtc,
                    r.Header.UpdatedAtUtc,
                    isNew: false
                ));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ApplyFilter()
    {
        Rows.Clear();

        IEnumerable<WebLoginRowVm> filtered = _all;

        var q = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Username.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in filtered)
            Rows.Add(r);
    }
}
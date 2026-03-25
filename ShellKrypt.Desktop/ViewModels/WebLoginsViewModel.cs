using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using Avalonia.Threading;
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
    [ObservableProperty] private string url;
    [ObservableProperty] private string notes;
    [ObservableProperty] private string twoFaNote;
    [ObservableProperty] private string totpSecret;
    [ObservableProperty] private string totpCode = "";
    [ObservableProperty] private string totpCountdown = "";
    [ObservableProperty] private string totpStatus = "No TOTP configured";
    [ObservableProperty] private bool isFavorite;

    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isPasswordVisible;

    private string _origTitle = "";
    private string _origUsername = "";
    private string _origPassword = "";
    private string _origUrl = "";
    private string _origNotes = "";
    private string _origTwoFaNote = "";
    private string _origTotpSecret = "";
    private bool _origFavorite;

    public WebLoginRowVm(
        string id,
        string title,
        string username,
        string password,
        string url,
        string notes,
        string twoFaNote,
        string totpSecret,
        bool favorite,
        string createdAtUtc,
        string updatedAtUtc,
        bool isNew)
    {
        Id = id;
        Title = title ?? "";
        Username = username ?? "";
        Password = password ?? "";
        Url = url ?? "";
        Notes = notes ?? "";
        TwoFaNote = twoFaNote ?? "";
        TotpSecret = totpSecret ?? "";
        IsFavorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsNew = isNew;
    }

    public string IconLetter => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[0].ToString().ToUpperInvariant();
    public string PasswordDisplay => IsPasswordVisible ? Password : "**********";
    public string UrlDisplay => DisplayOrPlaceholder("URL", Url);
    public string NotesDisplay => DisplayOrPlaceholder("Notes", Notes);
    public string TwoFaNoteDisplay => DisplayOrPlaceholder("2FA", TwoFaNote);
    public string FavoriteGlyph => IsFavorite ? "*" : "";
    public bool HasTotp => !string.IsNullOrWhiteSpace(TotpSecret);
    public bool CanCopyTotp => !string.IsNullOrWhiteSpace(TotpCode);

    public bool IsViewing => !IsEditing;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsViewing));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnUrlChanged(string value) => OnPropertyChanged(nameof(UrlDisplay));
    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(NotesDisplay));
    partial void OnTwoFaNoteChanged(string value) => OnPropertyChanged(nameof(TwoFaNoteDisplay));
    partial void OnTotpSecretChanged(string value)
    {
        OnPropertyChanged(nameof(HasTotp));
        OnPropertyChanged(nameof(CanCopyTotp));
    }
    partial void OnTotpCodeChanged(string value)
    {
        OnPropertyChanged(nameof(CanCopyTotp));
        OnPropertyChanged(nameof(TotpStatus));
    }
    partial void OnTotpCountdownChanged(string value) => OnPropertyChanged(nameof(TotpStatus));
    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteGlyph));
    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(PasswordDisplay));

    public void BeginEdit()
    {
        _origTitle = Title;
        _origUsername = Username;
        _origPassword = Password;
        _origUrl = Url;
        _origNotes = Notes;
        _origTwoFaNote = TwoFaNote;
        _origTotpSecret = TotpSecret;
        _origFavorite = IsFavorite;
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
        Url = _origUrl;
        Notes = _origNotes;
        TwoFaNote = _origTwoFaNote;
        TotpSecret = _origTotpSecret;
        IsFavorite = _origFavorite;
        IsEditing = false;
    }

    public void MarkSaved()
    {
        IsNew = false;
        IsEditing = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
    }

    public void RefreshTotpState(DateTimeOffset now)
    {
        if (!HasTotp)
        {
            TotpCode = "";
            TotpCountdown = "";
            TotpStatus = "No TOTP configured";
            return;
        }

        if (TotpToolkit.TryGenerateCode(TotpSecret, now, out var code, out var secondsRemaining, out var error))
        {
            TotpCode = code;
            TotpCountdown = $"{secondsRemaining:00}s";
            TotpStatus = $"OTP: {code} ({secondsRemaining:00}s)";
            return;
        }

        TotpCode = "";
        TotpCountdown = "";
        TotpStatus = string.IsNullOrWhiteSpace(error) ? "Invalid TOTP secret" : error;
    }

    private static string DisplayOrPlaceholder(string label, string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        return text.Length > 120 ? $"{label}: {text[..117]}..." : $"{label}: {text}";
    }
}

public partial class WebLoginsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;
    private readonly DispatcherTimer _totpTimer = new();

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
        _totpTimer.Interval = TimeSpan.FromSeconds(1);
        _totpTimer.Tick += (_, _) => RefreshTotpRows();
        _totpTimer.Start();
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
            url: "",
            notes: "",
            twoFaNote: "",
            totpSecret: "",
            favorite: false,
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

        var totpSecret = row.TotpSecret?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(totpSecret) &&
            !TotpToolkit.TryParse(totpSecret, out _, out var totpError))
        {
            Error = $"TOTP secret is invalid: {totpError}";
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var payload = new WebPayload(
                Title: row.Title,
                Url: row.Url ?? "",
                Username: row.Username,
                Password: row.Password,
                Notes: row.Notes ?? "",
                TwoFaNote: row.TwoFaNote ?? "",
                TotpSecret: totpSecret
            );

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);

            var header = new VaultItemHeader(
                Id: row.Id,
                Type: ItemType.Web,
                Favorite: row.IsFavorite,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: now
            );

            if (row.IsNew)
                await _repo.InsertAsync(_root.VaultPath, header, enc);
            else
                await _repo.UpdateAsync(_root.VaultPath, header, enc);

            row.MarkSaved();
            row.RefreshTotpState(DateTimeOffset.UtcNow);
            ApplyFilter();
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
        ApplyFilter();
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

    [RelayCommand]
    private async Task ToggleFavoriteAsync(WebLoginRowVm row)
    {
        Error = "";
        var previous = row.IsFavorite;
        row.IsFavorite = !row.IsFavorite;
        await SaveAsync(row);

        if (!string.IsNullOrWhiteSpace(Error))
            row.IsFavorite = previous;
    }

    [RelayCommand]
    private async Task CopyTotpAsync(WebLoginRowVm row)
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(row.TotpCode))
        {
            Error = "No TOTP code available.";
            return;
        }

        await _root.CopyToClipboardAsync(row.TotpCode);
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
                    payload.Url,
                    payload.Notes,
                    payload.TwoFaNote,
                    payload.TotpSecret,
                    r.Header.Favorite,
                    r.Header.CreatedAtUtc,
                    r.Header.UpdatedAtUtc,
                    isNew: false
                ));
            }

            ApplyFilter();
            RefreshTotpRows();
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
                r.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Url.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TwoFaNote.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TotpSecret.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.IsFavorite && "favorite".Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var r in filtered)
            Rows.Add(r);
    }

    private void RefreshTotpRows()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var row in _all)
            row.RefreshTotpState(now);
    }
}

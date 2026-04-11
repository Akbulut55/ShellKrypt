using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
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
    [ObservableProperty] private string email;
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
    private string _origEmail = "";
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
        bool isNew,
        string email = "")
    {
        Id = id;
        Title = title ?? "";
        Username = username ?? "";
        Email = email ?? "";
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
    public string UsernameDisplay => string.IsNullOrWhiteSpace(Username) ? Email : Username;
    public string PasswordDisplay => IsPasswordVisible ? Password : "**********";
    public string UrlHost => FormatUrlHost(Url);
    public string UrlDisplay => DisplayOrPlaceholder("URL", Url);
    public string NotesDisplay => DisplayOrPlaceholder("Notes", Notes);
    public string TwoFaNoteDisplay => DisplayOrPlaceholder("2FA", TwoFaNote);
    public string FavoriteGlyph => IsFavorite ? "*" : "";
    public bool HasTotp => !string.IsNullOrWhiteSpace(TotpSecret);
    public bool CanCopyTotp => !string.IsNullOrWhiteSpace(TotpCode);

    public bool IsViewing => !IsEditing;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsViewing));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnUsernameChanged(string value) => OnPropertyChanged(nameof(UsernameDisplay));
    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(UsernameDisplay));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnUrlChanged(string value)
    {
        OnPropertyChanged(nameof(UrlHost));
        OnPropertyChanged(nameof(UrlDisplay));
    }
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
        _origEmail = Email;
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
        Email = _origEmail;
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

    private static string FormatUrlHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "no url";

        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Host))
            return absolute.Host;

        var withoutScheme = text
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase);

        var slash = withoutScheme.IndexOf('/');
        return slash < 0 ? withoutScheme : withoutScheme[..slash];
    }
}

public sealed class EmailFilterOptionVm
{
    public EmailFilterOptionVm(string email, IRelayCommand<string> selectCommand)
    {
        Email = email;
        SelectCommand = selectCommand;
    }

    public string Email { get; }
    public IRelayCommand<string> SelectCommand { get; }
}

public partial class WebLoginsViewModel : ViewModelBase
{
    private const int PageSize = 5;
    private const int GeneratedLoginPasswordLength = 32;

    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;
    private readonly DispatcherTimer _totpTimer = new();

    private readonly List<WebLoginRowVm> _all = new();
    private readonly List<WebLoginRowVm> _filtered = new();
    private WebLoginRowVm? _selectedDetailsRow;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly char[] LoginPasswordChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

    public ObservableCollection<WebLoginRowVm> Rows { get; } = new();
    public ObservableCollection<EmailFilterOptionVm> EmailFilterOptions { get; } = new();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedEmailFilter = "";
    [ObservableProperty] private bool isEmailFilterPopupOpen;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private bool isAddWebLoginModalOpen;
    [ObservableProperty] private bool isAddWebLoginMode = true;
    [ObservableProperty] private bool isLoginDetailsEditing;
    [ObservableProperty] private bool isLoginDeleteConfirming;
    [ObservableProperty] private bool isAddPasswordVisible;
    [ObservableProperty] private string addTitle = "";
    [ObservableProperty] private string addUrl = "";
    [ObservableProperty] private string addUsername = "";
    [ObservableProperty] private string addEmail = "";
    [ObservableProperty] private string addPassword = "";
    [ObservableProperty] private string addTotpSecret = "";
    [ObservableProperty] private string addNotes = "";

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
    public string ItemsSummary
    {
        get
        {
            var hasActiveFilter =
                !string.IsNullOrWhiteSpace(SearchText) ||
                !string.IsNullOrWhiteSpace(SelectedEmailFilter);
            var count = hasActiveFilter ? _filtered.Count : _all.Count;
            var label = count == 1 ? "item" : "items";
            if (!string.IsNullOrWhiteSpace(SelectedEmailFilter))
                return $"{count} {label} for {SelectedEmailFilter}";

            return string.IsNullOrWhiteSpace(SearchText)
                ? $"{count} total {label} stored in your vault"
                : $"{count} matching {label} found";
        }
    }
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public bool HasEmailFilterOptions => EmailFilterOptions.Count > 0;
    public bool HasSelectedEmailFilter => !string.IsNullOrWhiteSpace(SelectedEmailFilter);
    public string EmailFilterButtonText => HasSelectedEmailFilter ? "FILTERED" : "FILTER";
    public string EmailFilterSummary => HasSelectedEmailFilter
        ? $"Showing logins for {SelectedEmailFilter}"
        : "Choose an email to filter logins";
    public string AddModalTitle => IsAddWebLoginMode
        ? "Add Web Login"
        : IsLoginDeleteConfirming
            ? "Delete Login?"
            : IsLoginDetailsEditing
                ? "Edit Login"
                : "Login Details";
    public string AddModalSubtitle => IsAddWebLoginMode
        ? "Store a new website credential in your encrypted vault."
        : IsLoginDeleteConfirming
            ? "Are you sure you want to delete this login? This action cannot be undone."
            : IsLoginDetailsEditing
                ? "Update the saved credential stored in this encrypted vault."
                : "Review the saved credential stored in this encrypted vault.";
    public bool IsDetailsViewMode => !IsAddWebLoginMode && !IsLoginDetailsEditing && !IsLoginDeleteConfirming;
    public bool IsDetailsEditMode => !IsAddWebLoginMode && IsLoginDetailsEditing && !IsLoginDeleteConfirming;
    public bool IsDetailsDeleteConfirmMode => !IsAddWebLoginMode && IsLoginDeleteConfirming;
    public bool IsAddFormReadOnly => !IsAddWebLoginMode && !IsLoginDetailsEditing;
    public bool CanGenerateModalPassword => IsAddWebLoginMode || IsDetailsEditMode;
    public string AddModalFooterText => IsDetailsDeleteConfirmMode
        ? $"Are you sure you want to delete \"{(string.IsNullOrWhiteSpace(AddTitle) ? "this login" : AddTitle)}\"?"
        : "Fields are encrypted locally before being stored.";
    public string AddPasswordVisibilityLabel => IsAddPasswordVisible ? "Hide" : "Reveal";

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
    partial void OnSelectedEmailFilterChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedEmailFilter));
        OnPropertyChanged(nameof(EmailFilterButtonText));
        OnPropertyChanged(nameof(EmailFilterSummary));
        ApplyFilter();
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
    }
    partial void OnIsAddPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(AddPasswordVisibilityLabel));
    partial void OnIsAddWebLoginModeChanged(bool value)
        => NotifyModalModeChanged();

    partial void OnIsLoginDetailsEditingChanged(bool value)
        => NotifyModalModeChanged();

    partial void OnIsLoginDeleteConfirmingChanged(bool value)
        => NotifyModalModeChanged();

    partial void OnAddTitleChanged(string value)
        => OnPropertyChanged(nameof(AddModalFooterText));

    private void NotifyModalModeChanged()
    {
        OnPropertyChanged(nameof(AddModalTitle));
        OnPropertyChanged(nameof(AddModalSubtitle));
        OnPropertyChanged(nameof(IsDetailsViewMode));
        OnPropertyChanged(nameof(IsDetailsEditMode));
        OnPropertyChanged(nameof(IsDetailsDeleteConfirmMode));
        OnPropertyChanged(nameof(IsAddFormReadOnly));
        OnPropertyChanged(nameof(CanGenerateModalPassword));
        OnPropertyChanged(nameof(AddModalFooterText));
    }

    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        IsEmailFilterPopupOpen = false;
        _selectedDetailsRow = null;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddWebLoginMode = true;
        ClearAddForm();
        IsAddWebLoginModalOpen = true;
    }

    [RelayCommand]
    private void BeginEdit(WebLoginRowVm row)
    {
        Error = "";
        row.BeginEdit();
    }

    [RelayCommand]
    private void ShowDetails(WebLoginRowVm row)
    {
        Error = "";
        IsEmailFilterPopupOpen = false;
        _selectedDetailsRow = row;
        IsAddWebLoginMode = false;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        PopulateModalFromRow(row);
        IsAddPasswordVisible = false;
        IsAddWebLoginModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsLoginDeleteConfirming = false;
        IsLoginDetailsEditing = true;
    }

    [RelayCommand]
    private void CancelDetailsEdit()
    {
        Error = "";

        if (_selectedDetailsRow is not null)
            PopulateModalFromRow(_selectedDetailsRow);

        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddPasswordVisible = false;
    }

    [RelayCommand]
    private void BeginDetailsDelete()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = true;
        IsAddPasswordVisible = false;
    }

    [RelayCommand]
    private void CancelDetailsDelete()
    {
        Error = "";
        IsLoginDeleteConfirming = false;
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
            )
            {
                Email = row.Email.Trim()
            };

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

    [RelayCommand]
    private async Task CopyPasswordAsync(WebLoginRowVm row)
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(row.Password))
        {
            Error = "No password available.";
            return;
        }

        await _root.CopyToClipboardAsync(row.Password);
    }

    [RelayCommand]
    private void CancelAdd()
    {
        Error = "";
        ClearAddForm();
        _selectedDetailsRow = null;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddWebLoginModalOpen = false;
    }

    [RelayCommand]
    private void ToggleAddPasswordVisibility()
    {
        IsAddPasswordVisible = !IsAddPasswordVisible;
    }

    [RelayCommand]
    private void GenerateAddPassword()
    {
        var chars = new char[GeneratedLoginPasswordLength];

        for (var i = 0; i < chars.Length; i++)
            chars[i] = LoginPasswordChars[RandomNumberGenerator.GetInt32(LoginPasswordChars.Length)];

        AddPassword = new string(chars);
        IsAddPasswordVisible = true;
        Error = "";
    }

    [RelayCommand]
    private async Task CopyAddPasswordAsync()
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(AddPassword))
        {
            Error = "No generated password available.";
            return;
        }

        await _root.CopyToClipboardAsync(AddPassword);
    }

    [RelayCommand]
    private async Task SaveAddAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var totpSecret = AddTotpSecret.Trim();
        if (!string.IsNullOrWhiteSpace(totpSecret) &&
            !TotpToolkit.TryParse(totpSecret, out _, out var totpError))
        {
            Error = $"TOTP secret is invalid: {totpError}";
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var id = Guid.NewGuid().ToString("N");
            var payload = new WebPayload(
                Title: AddTitle.Trim(),
                Url: AddUrl.Trim(),
                Username: AddUsername.Trim(),
                Password: AddPassword,
                Notes: AddNotes.Trim(),
                TwoFaNote: "",
                TotpSecret: totpSecret
            )
            {
                Email = AddEmail.Trim()
            };

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);
            var header = new VaultItemHeader(
                Id: id,
                Type: ItemType.Web,
                Favorite: false,
                CreatedAtUtc: now,
                UpdatedAtUtc: now
            );

            await _repo.InsertAsync(_root.VaultPath, header, enc);

            var row = new WebLoginRowVm(
                id,
                payload.Title,
                payload.Username,
                payload.Password,
                payload.Url,
                payload.Notes,
                payload.TwoFaNote,
                payload.TotpSecret,
                favorite: false,
                createdAtUtc: now,
                updatedAtUtc: now,
                isNew: false,
                email: payload.Email
            );

            row.RefreshTotpState(DateTimeOffset.UtcNow);
            _all.Insert(0, row);
            RefreshEmailFilterOptions();
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveDetailsAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = "No login selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var totpSecret = AddTotpSecret.Trim();
        if (!string.IsNullOrWhiteSpace(totpSecret) &&
            !TotpToolkit.TryParse(totpSecret, out _, out var totpError))
        {
            Error = $"TOTP secret is invalid: {totpError}";
            return;
        }

        try
        {
            var row = _selectedDetailsRow;
            var now = DateTimeOffset.UtcNow.ToString("O");
            var payload = new WebPayload(
                Title: AddTitle.Trim(),
                Url: AddUrl.Trim(),
                Username: AddUsername.Trim(),
                Password: AddPassword,
                Notes: AddNotes.Trim(),
                TwoFaNote: row.TwoFaNote ?? "",
                TotpSecret: totpSecret
            )
            {
                Email = AddEmail.Trim()
            };

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);
            var header = new VaultItemHeader(
                Id: row.Id,
                Type: ItemType.Web,
                Favorite: row.IsFavorite,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: now
            );

            await _repo.UpdateAsync(_root.VaultPath, header, enc);

            row.Title = payload.Title;
            row.Url = payload.Url;
            row.Username = payload.Username;
            row.Email = payload.Email;
            row.Password = payload.Password;
            row.Notes = payload.Notes;
            row.TwoFaNote = payload.TwoFaNote;
            row.TotpSecret = payload.TotpSecret;
            row.MarkSaved();
            row.RefreshTotpState(DateTimeOffset.UtcNow);

            IsLoginDetailsEditing = false;
            IsLoginDeleteConfirming = false;
            RefreshEmailFilterOptions();
            ApplyFilter(resetPage: false);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConfirmDetailsDeleteAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = "No login selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            var row = _selectedDetailsRow;
            await _repo.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
            _selectedDetailsRow = null;
            IsLoginDeleteConfirming = false;
            IsLoginDetailsEditing = false;
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(WebLoginRowVm row)
    {
        _all.Remove(row);
        RefreshEmailFilterOptions();
        ApplyFilter(resetPage: false);
    }

    [RelayCommand]
    private void ToggleEmailFilter()
    {
        Error = "";
        RefreshEmailFilterOptions();
        IsEmailFilterPopupOpen = !IsEmailFilterPopupOpen;
    }

    [RelayCommand]
    private void SelectEmailFilter(string email)
    {
        SelectedEmailFilter = email?.Trim() ?? "";
        IsEmailFilterPopupOpen = false;
    }

    [RelayCommand]
    private void ClearEmailFilter()
    {
        SelectedEmailFilter = "";
        IsEmailFilterPopupOpen = false;
    }

    private void ClearAddForm()
    {
        AddTitle = "";
        AddUrl = "";
        AddUsername = "";
        AddEmail = "";
        AddPassword = "";
        AddTotpSecret = "";
        AddNotes = "";
        IsAddPasswordVisible = false;
    }

    private void PopulateModalFromRow(WebLoginRowVm row)
    {
        AddTitle = row.Title;
        AddUrl = row.Url;
        AddUsername = row.Username;
        AddEmail = row.Email;
        AddPassword = row.Password;
        AddTotpSecret = row.TotpSecret;
        AddNotes = row.Notes;
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
                    isNew: false,
                    email: payload.Email
                ));
            }

            RefreshEmailFilterOptions();
            ApplyFilter();
            RefreshTotpRows();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void ApplyFilter()
        => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        IEnumerable<WebLoginRowVm> filtered = _all;

        var q = SearchText?.Trim();
        var selectedEmail = SelectedEmailFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedEmail))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.Email?.Trim(), selectedEmail, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Url.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TwoFaNote.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TotpSecret.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.IsFavorite && "favorite".Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        RenderPage();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var r in _filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            Rows.Add(r);

        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void RefreshEmailFilterOptions()
    {
        var selected = SelectedEmailFilter;
        EmailFilterOptions.Clear();

        foreach (var email in _all
                     .Select(row => row.Email?.Trim())
                     .Where(email => !string.IsNullOrWhiteSpace(email))
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(email => email, StringComparer.OrdinalIgnoreCase))
        {
            EmailFilterOptions.Add(new EmailFilterOptionVm(email, SelectEmailFilterCommand));
        }

        if (!string.IsNullOrWhiteSpace(selected) &&
            !EmailFilterOptions.Any(option => string.Equals(option.Email, selected, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedEmailFilter = "";
        }

        OnPropertyChanged(nameof(HasEmailFilterOptions));
        OnPropertyChanged(nameof(EmailFilterSummary));
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void PreviousPage()
    {
        if (!CanGoPreviousPage)
            return;

        CurrentPage--;
        RenderPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage()
    {
        if (!CanGoNextPage)
            return;

        CurrentPage++;
        RenderPage();
    }

    private void RefreshTotpRows()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var row in _all)
            row.RefreshTotpState(now);
    }
}

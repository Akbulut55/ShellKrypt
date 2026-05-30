using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel : ViewModelBase
{
    private const int PageSize = 5;
    private const int GeneratedLoginPasswordLength = 32;
    private const string AllUsernameFilter = "Username: All";
    private const string AllEmailFilter = "Email: All";
    private const string SortNewest = "Sort: Newest";
    private const string SortWebsite = "Website";
    private const string SortAlphabetical = "Alphabetical";

    private readonly MainWindowViewModel _root;
    private readonly IWebLoginService _webLoginService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;

    private readonly List<WebLoginRowVm> _all = new();
    private readonly List<WebLoginRowVm> _filtered = new();
    private WebLoginRowVm? _selectedDetailsRow;

    private static readonly char[] LoginPasswordChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

    public ObservableCollection<WebLoginRowVm> Rows { get; } = new();
    public ObservableCollection<string> UsernameFilters { get; } = new() { AllUsernameFilter };
    public ObservableCollection<string> EmailFilters { get; } = new() { AllEmailFilter };
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        SortNewest,
        SortWebsite,
        SortAlphabetical
    };

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedUsernameFilter = "";
    [ObservableProperty] private string selectedUsernameFilterChoice = AllUsernameFilter;
    [ObservableProperty] private string selectedEmailFilter = "";
    [ObservableProperty] private string selectedEmailFilterChoice = AllEmailFilter;
    [ObservableProperty] private string selectedSortOption = SortNewest;
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
    [ObservableProperty] private string addNotes = "";

    public int TotalLoginsCount => _all.Count;
    public int ReusedPasswordCount => _all
        .Where(row => !string.IsNullOrWhiteSpace(row.Password))
        .GroupBy(row => row.Password, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Sum(group => group.Count());
    public int WeakPasswordCount => _all.Count(row => IsWeakPassword(row.Password));
    public string TotalLoginsSummary => TotalLoginsCount == 1
        ? "1 encrypted login in this vault"
        : $"{TotalLoginsCount} encrypted logins in this vault";
    public string ReusedPasswordSummary => ReusedPasswordCount == 1
        ? "1 login reuses a saved password"
        : $"{ReusedPasswordCount} logins reuse saved passwords";
    public string WeakPasswordSummary => WeakPasswordCount == 1
        ? "1 login uses a weak password"
        : $"{WeakPasswordCount} logins use weak passwords";
    public int TotalPages => DesktopPagination.GetTotalPages(_filtered.Count, PageSize);
    public string ItemsSummary => $"Showing {Rows.Count} of {_filtered.Count} web logins";
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public bool HasRows => Rows.Count > 0;
    public string EmptyTableTitle => _all.Count == 0
        ? "No web logins saved yet"
        : "No web logins match this view";
    public string EmptyTableSubtitle => _all.Count == 0
        ? "Add a website login to start storing encrypted credentials in this vault."
        : "Adjust the search term, username filter, or email filter to show more saved logins.";
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

    public WebLoginsViewModel(MainWindowViewModel root, IWebLoginService webLoginService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _webLoginService = webLoginService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedUsernameFilterChoiceChanged(string value)
    {
        var choice = value?.Trim() ?? AllUsernameFilter;
        SelectedUsernameFilter = string.Equals(choice, AllUsernameFilter, StringComparison.OrdinalIgnoreCase)
            ? ""
            : choice;
    }

    partial void OnSelectedUsernameFilterChanged(string value)
    {
        var expectedChoice = string.IsNullOrWhiteSpace(value) ? AllUsernameFilter : value.Trim();
        if (!string.Equals(SelectedUsernameFilterChoice, expectedChoice, StringComparison.Ordinal))
            SelectedUsernameFilterChoice = expectedChoice;

        ApplyFilter();
    }

    partial void OnSelectedEmailFilterChoiceChanged(string value)
    {
        var choice = value?.Trim() ?? AllEmailFilter;
        SelectedEmailFilter = string.Equals(choice, AllEmailFilter, StringComparison.OrdinalIgnoreCase)
            ? ""
            : choice;
    }

    partial void OnSelectedEmailFilterChanged(string value)
    {
        var expectedChoice = string.IsNullOrWhiteSpace(value) ? AllEmailFilter : value.Trim();
        if (!string.Equals(SelectedEmailFilterChoice, expectedChoice, StringComparison.Ordinal))
            SelectedEmailFilterChoice = expectedChoice;

        ApplyFilter();
    }
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();

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
        _selectedDetailsRow = null;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddWebLoginMode = true;
        ClearAddForm();
        IsAddWebLoginModalOpen = true;
    }

    [RelayCommand]
    private void ShowDetails(WebLoginRowVm row)
    {
        Error = "";
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
    private void TogglePassword(WebLoginRowVm row)
    {
        row.IsPasswordVisible = !row.IsPasswordVisible;
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
        _root.LogActivity("web", "Web login password copied", $"Copied password for {row.Title}.", "info", affectedItem: row.Title);
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
        AddPassword = GenerateStrongPassword();
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

        try
        {
            var entry = await _webLoginService.AddAsync(_root.VaultPath, _root.VaultKey, BuildInput());

            _all.Insert(0, ToRow(entry));
            await _refreshAllItemsAsync(entry.Id);
            RefreshLoginFilters();
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
            ApplyFilter();
            _root.LogActivity("web", "Web login added", $"Added {entry.Title}.", "success", affectedItem: entry.Title);
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

        try
        {
            var row = _selectedDetailsRow;
            var entry = await _webLoginService.UpdateAsync(
                _root.VaultPath,
                _root.VaultKey,
                row.Id,
                row.CreatedAtUtc,
                BuildInput());

            ApplyEntry(row, entry);
            await _refreshAllItemsAsync(entry.Id);

            IsLoginDetailsEditing = false;
            IsLoginDeleteConfirming = false;
            RefreshLoginFilters();
            ApplyFilter(resetPage: false);
            _root.LogActivity("web", "Web login updated", $"Updated {entry.Title}.", "info", affectedItem: entry.Title);
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
            await _webLoginService.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
            await _refreshAllItemsAsync(null);
            _selectedDetailsRow = null;
            IsLoginDeleteConfirming = false;
            IsLoginDetailsEditing = false;
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
            _root.LogActivity("web", "Web login deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(WebLoginRowVm row)
    {
        _all.Remove(row);
        RefreshLoginFilters();
        ApplyFilter(resetPage: false);
    }

    private void ClearAddForm()
    {
        AddTitle = "";
        AddUrl = "";
        AddUsername = "";
        AddEmail = "";
        AddPassword = "";
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
        AddNotes = row.Notes;
    }

    private WebLoginInput BuildInput()
        => new(AddTitle, AddUrl, AddUsername, AddEmail, AddPassword, AddNotes);

    private static WebLoginRowVm ToRow(WebLoginEntry entry)
        => new(
            entry.Id,
            entry.Title,
            entry.Username,
            entry.Password,
            entry.Url,
            entry.Notes,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            isNew: false,
            email: entry.Email);

    private static void ApplyEntry(WebLoginRowVm row, WebLoginEntry entry)
    {
        row.Title = entry.Title;
        row.Url = entry.Url;
        row.Username = entry.Username;
        row.Email = entry.Email;
        row.Password = entry.Password;
        row.Notes = entry.Notes;
        row.MarkSaved(entry.UpdatedAtUtc);
    }

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            _all.Clear();
            Rows.Clear();

            var entries = await _webLoginService.ListAsync(_root.VaultPath, _root.VaultKey);
            _all.AddRange(entries.Select(ToRow));

            RefreshLoginFilters();
            ApplyFilter();
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
        var selectedUsername = SelectedUsernameFilter?.Trim();
        var selectedEmail = SelectedEmailFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedUsername))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.Username?.Trim(), selectedUsername, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(selectedEmail))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.Email?.Trim(), selectedEmail, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Url.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.UrlHost.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedSortOption switch
        {
            SortWebsite => filtered
                .OrderBy(row => row.UrlHost, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
            SortAlphabetical => filtered.OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(row => ParseTimestamp(row.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = DesktopPagination.ClampPage(CurrentPage, _filtered.Count, PageSize);

        RenderPage();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var r in DesktopPagination.Page(_filtered, CurrentPage, PageSize))
            Rows.Add(r);

        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(EmptyTableTitle));
        OnPropertyChanged(nameof(EmptyTableSubtitle));
        NotifySummaryChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void RefreshLoginFilters()
    {
        var selectedUsername = SelectedUsernameFilter;
        var selected = SelectedEmailFilter;
        DesktopFilterOptions.RebuildStringOptions(UsernameFilters, AllUsernameFilter, _all.Select(row => row.Username));
        DesktopFilterOptions.RebuildStringOptions(EmailFilters, AllEmailFilter, _all.Select(row => row.Email));

        SelectedUsernameFilter = DesktopFilterOptions.KeepSelectedOrEmpty(UsernameFilters, selectedUsername);
        SelectedEmailFilter = DesktopFilterOptions.KeepSelectedOrEmpty(EmailFilters, selected);

        SelectedUsernameFilterChoice = string.IsNullOrWhiteSpace(SelectedUsernameFilter)
            ? AllUsernameFilter
            : SelectedUsernameFilter;

        SelectedEmailFilterChoice = string.IsNullOrWhiteSpace(SelectedEmailFilter)
            ? AllEmailFilter
            : SelectedEmailFilter;

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

    public async Task<bool> OpenForRemediationAsync(string itemId, bool generateReplacementPassword)
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(itemId) || _root.VaultPath is null)
            return false;

        if (_all.Count == 0)
            await LoadAsync();

        var row = _all.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
        if (row is null)
        {
            await LoadAsync();
            row = _all.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
            if (row is null)
                return false;
        }

        SearchText = "";
        SelectedUsernameFilter = "";
        SelectedUsernameFilterChoice = AllUsernameFilter;
        SelectedEmailFilter = "";
        SelectedEmailFilterChoice = AllEmailFilter;

        var index = _all.FindIndex(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
        CurrentPage = index < 0 ? 1 : (index / PageSize) + 1;
        RenderPage();

        _selectedDetailsRow = row;
        IsAddWebLoginMode = false;
        IsLoginDeleteConfirming = false;
        IsLoginDetailsEditing = true;
        PopulateModalFromRow(row);

        if (generateReplacementPassword)
        {
            AddPassword = GenerateStrongPassword();
            IsAddPasswordVisible = true;
        }
        else
        {
            IsAddPasswordVisible = false;
        }

        IsAddWebLoginModalOpen = true;
        return true;
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(TotalLoginsCount));
        OnPropertyChanged(nameof(ReusedPasswordCount));
        OnPropertyChanged(nameof(WeakPasswordCount));
        OnPropertyChanged(nameof(TotalLoginsSummary));
        OnPropertyChanged(nameof(ReusedPasswordSummary));
        OnPropertyChanged(nameof(WeakPasswordSummary));
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static bool IsWeakPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        var hasLower = Regex.IsMatch(password, "[a-z]");
        var hasUpper = Regex.IsMatch(password, "[A-Z]");
        var hasDigit = Regex.IsMatch(password, "[0-9]");
        var hasSymbol = Regex.IsMatch(password, "[^a-zA-Z0-9]");
        var classCount = new[] { hasLower, hasUpper, hasDigit, hasSymbol }.Count(value => value);

        return password.Length < 12 || classCount < 3;
    }

    private static string GenerateStrongPassword(int length = GeneratedLoginPasswordLength)
    {
        var chars = new char[length];

        for (var i = 0; i < chars.Length; i++)
            chars[i] = LoginPasswordChars[RandomNumberGenerator.GetInt32(LoginPasswordChars.Length)];

        return new string(chars);
    }

}

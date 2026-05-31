using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
}

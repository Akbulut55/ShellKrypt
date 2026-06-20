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
        ? T(_root, "WebLogins.Summary.TotalOne")
        : T(_root, "WebLogins.Summary.TotalMany", TotalLoginsCount);
    public string ReusedPasswordSummary => ReusedPasswordCount == 1
        ? T(_root, "WebLogins.Summary.ReusedOne")
        : T(_root, "WebLogins.Summary.ReusedMany", ReusedPasswordCount);
    public string WeakPasswordSummary => WeakPasswordCount == 1
        ? T(_root, "WebLogins.Summary.WeakOne")
        : T(_root, "WebLogins.Summary.WeakMany", WeakPasswordCount);
    public string ItemsSummary => T(_root, "WebLogins.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public string EmptyTableTitle => _all.Count == 0
        ? T(_root, "WebLogins.Empty.NoneTitle")
        : T(_root, "WebLogins.Empty.NoMatchTitle");
    public string EmptyTableSubtitle => _all.Count == 0
        ? T(_root, "WebLogins.Empty.NoneSubtitle")
        : T(_root, "WebLogins.Empty.NoMatchSubtitle");
    public string AddModalTitle => IsAddWebLoginMode
        ? T(_root, "WebLogins.Modal.AddTitle")
        : IsLoginDeleteConfirming
            ? T(_root, "WebLogins.Modal.DeleteTitle")
            : IsLoginDetailsEditing
                ? T(_root, "WebLogins.Modal.EditTitle")
                : T(_root, "WebLogins.Modal.DetailsTitle");
    public string AddModalSubtitle => IsAddWebLoginMode
        ? T(_root, "WebLogins.Modal.AddSubtitle")
        : IsLoginDeleteConfirming
            ? T(_root, "WebLogins.Modal.DeleteSubtitle")
            : IsLoginDetailsEditing
                ? T(_root, "WebLogins.Modal.EditSubtitle")
                : T(_root, "WebLogins.Modal.DetailsSubtitle");
    public bool IsDetailsViewMode => !IsAddWebLoginMode && !IsLoginDetailsEditing && !IsLoginDeleteConfirming;
    public bool IsDetailsEditMode => !IsAddWebLoginMode && IsLoginDetailsEditing && !IsLoginDeleteConfirming;
    public bool IsDetailsDeleteConfirmMode => !IsAddWebLoginMode && IsLoginDeleteConfirming;
    public bool IsAddFormReadOnly => !IsAddWebLoginMode && !IsLoginDetailsEditing;
    public bool CanGenerateModalPassword => IsAddWebLoginMode || IsDetailsEditMode;
    public string AddModalFooterText => IsDetailsDeleteConfirmMode
        ? T(_root, "WebLogins.Modal.DeleteFooter", string.IsNullOrWhiteSpace(AddTitle) ? T(_root, "WebLogins.ThisLogin") : AddTitle)
        : T(_root, "WebLogins.Modal.Footer");
    public string AddPasswordVisibilityLabel => IsAddPasswordVisible ? T(_root, "Common.Hide") : T(_root, "Common.Reveal");

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

    public override void RefreshLocalization()
    {
        foreach (var row in _all)
            row.RefreshLocalization();

        NotifyLocalized(
            nameof(TotalLoginsSummary),
            nameof(ReusedPasswordSummary),
            nameof(WeakPasswordSummary),
            nameof(ItemsSummary),
            nameof(EmptyTableTitle),
            nameof(EmptyTableSubtitle),
            nameof(AddModalTitle),
            nameof(AddModalSubtitle),
            nameof(AddModalFooterText),
            nameof(AddPasswordVisibilityLabel));
    }
}

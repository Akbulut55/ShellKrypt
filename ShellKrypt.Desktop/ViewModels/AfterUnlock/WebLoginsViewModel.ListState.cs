using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel
{
    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }

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

        foreach (var row in DesktopPagination.Page(_filtered, CurrentPage, PageSize))
            Rows.Add(row);

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
        var selectedEmail = SelectedEmailFilter;
        DesktopFilterOptions.RebuildStringOptions(UsernameFilters, AllUsernameFilter, _all.Select(row => row.Username));
        DesktopFilterOptions.RebuildStringOptions(EmailFilters, AllEmailFilter, _all.Select(row => row.Email));

        SelectedUsernameFilter = DesktopFilterOptions.KeepSelectedOrEmpty(UsernameFilters, selectedUsername);
        SelectedEmailFilter = DesktopFilterOptions.KeepSelectedOrEmpty(EmailFilters, selectedEmail);

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
}

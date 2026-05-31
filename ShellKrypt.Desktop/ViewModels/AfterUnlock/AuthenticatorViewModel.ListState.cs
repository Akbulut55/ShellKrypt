using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private void SelectEntry(AuthenticatorAccountVm? entry)
    {
        if (entry is not null)
            SelectedEntry = entry;
    }

    [RelayCommand]
    private void OpenDetails()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsEditorModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsDetailsModalOpen = true;
    }

    [RelayCommand]
    private void CloseDetails()
    {
        Error = string.Empty;
        IsDetailsModalOpen = false;
    }

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_allEntries.Count == 0)
            await LoadAsync(itemId);

        var entry = _allEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (entry is null)
        {
            await LoadAsync(itemId);
            entry = _allEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            if (entry is null)
                return false;
        }

        SearchText = string.Empty;
        ApplyFilter(itemId);
        return true;
    }

    private async Task LoadAsync(string? selectEntryId = null)
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        IsBusy = true;
        try
        {
            _allEntries.Clear();
            var entries = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
            foreach (var entry in entries)
                _allEntries.Add(new AuthenticatorAccountVm(entry));

            RefreshSnapshots();
            ApplyFilter(selectEntryId);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter(string? selectEntryId = null)
    {
        var query = SearchText?.Trim();
        IEnumerable<AuthenticatorAccountVm> filtered = _allEntries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(entry =>
                entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.KeyTypeDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var snapshot = filtered.ToList();
        FilteredEntries.Clear();
        foreach (var entry in snapshot)
            FilteredEntries.Add(entry);

        var targetId = selectEntryId ?? SelectedEntry?.Id;
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            var target = snapshot.FirstOrDefault(entry => string.Equals(entry.Id, targetId, StringComparison.Ordinal));
            if (target is not null)
            {
                SelectedEntry = target;
                NotifyCountProperties();
                return;
            }
        }

        SelectedEntry = snapshot.FirstOrDefault();
        NotifyCountProperties();
    }

    private void NotifyCountProperties()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(RefreshingSoonCount));
        OnPropertyChanged(nameof(RecentlyUsedCount));
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
        OnPropertyChanged(nameof(DetailSubtitle));
        OnPropertyChanged(nameof(CanCopyCode));
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    private static bool IsRecentlyUsed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DateTimeOffset.TryParse(value, out var timestamp) &&
               timestamp >= DateTimeOffset.UtcNow.Subtract(RecentlyUsedWindow);
    }
}

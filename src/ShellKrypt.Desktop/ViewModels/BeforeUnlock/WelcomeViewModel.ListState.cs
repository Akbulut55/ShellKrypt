using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    partial void OnSelectedVaultChanged(VaultRecordVm? value)
    {
        if (value is null)
        {
            Status = Vaults.Count == 0
                ? T(_localization, "Welcome.Status.NoVaults")
                : T(_localization, "Welcome.Status.SelectVault");
            return;
        }

        Status = value.Exists
            ? T(_localization, "Welcome.Status.Selected", value.DisplayLabel)
            : T(_localization, "Welcome.Status.SelectedMissing", value.DisplayLabel);
    }

    private void ReloadVaults(string? selectPath = null)
    {
        IsBusy = true;
        Error = "";

        try
        {
            var registry = _vaultRegistry.Load();
            var selectedPath = NormalizePath(selectPath ?? _session.VaultPath);

            var vaults = registry.Vaults.Select(x => new VaultRecordVm(x, _localization)).ToArray();

            _allVaults.Clear();
            _allVaults.AddRange(vaults);

            RecentVaults.Clear();
            foreach (var vault in _vaultRegistry.ListRecentVaults())
                RecentVaults.Add(new VaultRecordVm(vault, _localization));

            ApplyFilters();

            SelectedVault = Vaults.FirstOrDefault(x => string.Equals(NormalizePath(x.VaultPath), selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? Vaults.FirstOrDefault(x => x.IsFavorite)
                ?? Vaults.FirstOrDefault();

            OnPropertyChanged(nameof(VaultCount));
            OnPropertyChanged(nameof(ExistingVaultCount));
            OnPropertyChanged(nameof(ExistingVaultCountDisplay));
            OnPropertyChanged(nameof(TotalStorageDisplay));
            if (vaults.Length == 0)
            {
                Status = T(_localization, "Welcome.Status.NoVaults");
            }
            else if (SelectedVault is not null)
            {
                Status = SelectedVault.Exists
                    ? T(_localization, vaults.Length == 1 ? "Welcome.Status.LoadedOne" : "Welcome.Status.LoadedMany", vaults.Length)
                    : T(_localization, "Welcome.Status.SelectedMissing", SelectedVault.DisplayLabel);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = T(_localization, "Welcome.Status.LoadFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        var selectedId = SelectedVault?.Id;
        IEnumerable<VaultRecordVm> items = _allVaults;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            items = items.Where(vault =>
                vault.DisplayLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                vault.DescriptionDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                vault.PathDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        items = ActiveSort switch
        {
            "name" => items
                .OrderByDescending(vault => vault.IsFavorite)
                .ThenBy(vault => vault.DisplayLabel, StringComparer.OrdinalIgnoreCase),
            _ => items
                .OrderByDescending(vault => vault.IsFavorite)
                .ThenByDescending(vault => DateTimeOffset.TryParse(vault.LastOpenedAtUtc, out var opened) ? opened : DateTimeOffset.MinValue)
                .ThenBy(vault => vault.DisplayLabel, StringComparer.OrdinalIgnoreCase)
        };

        var filteredItems = items.ToList();
        _filteredVaultCount = filteredItems.Count;

        var totalPages = TotalPages;
        if (CurrentPage > totalPages)
        {
            CurrentPage = totalPages;
            return;
        }

        items = filteredItems
            .Skip((CurrentPage - 1) * VaultPageSize)
            .Take(VaultPageSize);

        Vaults.Clear();
        foreach (var vault in items)
            Vaults.Add(vault);

        if (selectedId is not null)
            SelectedVault = Vaults.FirstOrDefault(vault => vault.Id == selectedId) ?? Vaults.FirstOrDefault();
        else if (SelectedVault is null)
            SelectedVault = Vaults.FirstOrDefault();

        OnPropertyChanged(nameof(HasVaults));
        OnPropertyChanged(nameof(VaultCount));
        OnPropertyChanged(nameof(ExistingVaultCount));
        OnPropertyChanged(nameof(ExistingVaultCountDisplay));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(TotalStorageDisplay));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetFullPath(path);
}

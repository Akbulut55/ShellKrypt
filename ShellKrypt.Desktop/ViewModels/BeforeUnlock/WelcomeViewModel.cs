using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel : ViewModelBase
{
    private const int VaultPageSize = 3;
    private readonly MainWindowViewModel _root;
    private readonly VaultRegistryStore _vaultRegistry;
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly List<VaultRecordVm> _allVaults = new();
    private int _filteredVaultCount;

    public ObservableCollection<VaultRecordVm> Vaults { get; } = new();
    public ObservableCollection<VaultRecordVm> RecentVaults { get; } = new();

    [ObservableProperty] private VaultRecordVm? selectedVault;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeSort = "recent";
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private string status = "Select a vault to unlock, or create a new one.";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDeleteOverlayOpen;
    [ObservableProperty] private bool isDeletePasswordStep;
    [ObservableProperty] private bool isDeletePasswordVisible;
    [ObservableProperty] private string deletePassword = "";
    [ObservableProperty] private string deleteOverlayError = "";
    [ObservableProperty] private VaultRecordVm? deleteTarget;
    [ObservableProperty] private bool isRemoveOverlayOpen;
    [ObservableProperty] private VaultRecordVm? removeTarget;

    public WelcomeViewModel(MainWindowViewModel root, VaultRegistryStore vaultRegistry)
    {
        _root = root;
        _vaultRegistry = vaultRegistry;
        ReloadVaults();
    }

    public bool IsRecentSortActive => ActiveSort == "recent";
    public bool IsNameSortActive => ActiveSort == "name";
    public bool HasVaults => Vaults.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasDeleteOverlayError => !string.IsNullOrWhiteSpace(DeleteOverlayError);
    public int VaultCount => _allVaults.Count;
    public int ExistingVaultCount => _allVaults.Count(vault => vault.Exists);
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredVaultCount / (double)VaultPageSize));
    public bool HasMultiplePages => TotalPages > 1;
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public string PageIndicator => $"{CurrentPage} / {TotalPages}";
    public string TotalStorageDisplay => FormatBytes(_allVaults.Where(vault => vault.Exists).Sum(vault => GetVaultSize(vault.VaultPath)));
    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No vaults added yet"
        : "No vaults match this search";
    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? "Create a new vault or import an existing vault file to get started."
        : "Try a different name, path fragment, or clear the current search.";
    public bool IsDeleteWarningStep => IsDeleteOverlayOpen && !IsDeletePasswordStep;
    public string DeleteWarningTitle => $"Permanently delete {DeleteTarget?.DisplayLabel ?? "vault"}?";
    public string DeletePasswordTitle => "Enter the master password to permanently delete this vault.";
    public string DeletePasswordDetail => DeleteTarget?.VaultPath ?? "";
    public string DeletePasswordVisibilityLabel => IsDeletePasswordVisible ? "Hide" : "Show";
    public string RemoveOverlayTitle => $"Remove {RemoveTarget?.DisplayLabel ?? "vault"} from the list?";
    public string RemoveOverlayDetail => "This only removes the stale entry from ShellKrypt's launcher. No vault file will be deleted.";

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnDeleteOverlayErrorChanged(string value) => OnPropertyChanged(nameof(HasDeleteOverlayError));

    partial void OnActiveSortChanged(string value)
    {
        OnPropertyChanged(nameof(IsRecentSortActive));
        OnPropertyChanged(nameof(IsNameSortActive));
        CurrentPage = 1;
        ApplyFilters();
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(PageIndicator));
        ApplyFilters();
    }

    partial void OnIsDeleteOverlayOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDeleteWarningStep));
    }

    partial void OnIsDeletePasswordStepChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDeleteWarningStep));
    }

    partial void OnIsDeletePasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(DeletePasswordVisibilityLabel));
    }

    partial void OnDeleteTargetChanged(VaultRecordVm? value)
    {
        OnPropertyChanged(nameof(DeleteWarningTitle));
        OnPropertyChanged(nameof(DeletePasswordDetail));
    }

    partial void OnRemoveTargetChanged(VaultRecordVm? value)
    {
        OnPropertyChanged(nameof(RemoveOverlayTitle));
    }

    [RelayCommand]
    private void CreateVault() => _root.GoCreateVault();

    [RelayCommand]
    private void Refresh() => ReloadVaults(SelectedVault?.VaultPath);

    [RelayCommand]
    private void SortByRecent() => ActiveSort = "recent";

    [RelayCommand]
    private void SortByName() => ActiveSort = "name";

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPreviousPage)
            CurrentPage--;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNextPage)
            CurrentPage++;
    }

    [RelayCommand]
    private void OpenSelectedVault()
    {
        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        OpenVault(SelectedVault);
    }

    [RelayCommand]
    private void OpenDefaultVault()
    {
        var defaultVault = _vaultRegistry.GetDefaultVault();
        if (defaultVault is null)
        {
            Error = "No default vault has been set yet.";
            return;
        }

        OpenVault(new VaultRecordVm(defaultVault));
    }

    [RelayCommand]
    private async Task ImportVaultAsync()
    {
        Error = "";

        try
        {
            var (confirmed, path, displayNameInput) = await _root.ShowImportVaultDialogAsync();
            if (!confirmed)
                return;

            path = VaultFileGuard.EnsureExistingVaultFile(path);
            if (!File.Exists(path))
            {
                Error = "That vault file does not exist.";
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(displayNameInput)
                ? Path.GetFileNameWithoutExtension(path)
                : displayNameInput.Trim();

            var entry = _vaultRegistry.UpsertVault(
                path,
                displayName,
                "",
                isDefault: !_vaultRegistry.ListVaults().Any(),
                markOpened: false);

            ReloadVaults(entry.VaultPath);
            Status = "Vault imported into the local manager.";
            _root.LogActivity("vault", "Vault added to launcher", $"Imported {displayName} into the local vault list.", "success", entry.VaultPath, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void DuplicateSelectedVault()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        if (!SelectedVault.Exists)
        {
            Error = "The selected vault file could not be found.";
            return;
        }

        try
        {
            var sourcePath = VaultFileGuard.EnsureExistingVaultFile(SelectedVault.VaultPath);
            var targetPath = VaultFileGuard.EnsureVaultFilePath(DefaultPaths.GetSuggestedVaultPath($"{SelectedVault.DisplayLabel} Copy"));
            VaultFileGuard.EnsureDifferentPaths(sourcePath, targetPath, "Vault duplicate target must be different from the source vault.");
            File.Copy(sourcePath, targetPath, overwrite: false);
            CopySidecarIfExists(sourcePath, targetPath, "-wal");
            CopySidecarIfExists(sourcePath, targetPath, "-shm");

            _vaultRegistry.UpsertVault(
                targetPath,
                $"{SelectedVault.DisplayLabel} Copy",
                SelectedVault.Description,
                isDefault: false,
                markOpened: false);

            ReloadVaults(targetPath);
            Status = "Vault duplicated.";
            _root.LogActivity("vault", "Vault duplicated", $"Created a duplicate of {SelectedVault.DisplayLabel}.", "success", targetPath, $"{SelectedVault.DisplayLabel} Copy");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedVaultAsync()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Remove Vault From List",
            $"Remove {SelectedVault.DisplayLabel} from the local vault list?",
            "This only removes the vault from ShellKrypt's local manager. The vault file stays on disk and can be added again later.",
            "Remove From List");

        if (!confirmed)
            return;

        try
        {
            var displayName = SelectedVault.DisplayLabel;
            var path = SelectedVault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            ReloadVaults();
            Status = $"{displayName} was removed from the local vault list.";
            _root.LogActivity("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void RemoveVaultFromList(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        RemoveTarget = vault;
        IsRemoveOverlayOpen = true;
    }

    [RelayCommand]
    private void CancelRemoveOverlay()
    {
        if (IsBusy)
            return;

        IsRemoveOverlayOpen = false;
        RemoveTarget = null;
    }

    [RelayCommand]
    private void ConfirmRemoveOverlay()
    {
        Error = "";

        var vault = RemoveTarget;
        if (vault is null)
        {
            IsRemoveOverlayOpen = false;
            return;
        }

        try
        {
            var displayName = vault.DisplayLabel;
            var path = vault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            IsRemoveOverlayOpen = false;
            RemoveTarget = null;
            ReloadVaults();
            Status = $"{displayName} was removed from the local vault list.";
            _root.LogActivity("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenVault(VaultRecordVm? vault)
    {
        if (vault is null)
            return;

        Error = "";

        if (!vault.Exists)
        {
            Status = $"Vault file is missing:\n{vault.VaultPath}";
            Error = "The selected vault file could not be found.";
            ReloadVaults(vault.VaultPath);
            return;
        }

        _root.SetVaultPath(vault.VaultPath);
        _root.GoUnlock();
    }

    [RelayCommand]
    private void DeleteVault(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        if (!vault.Exists)
        {
            Error = "The selected vault file could not be found.";
            return;
        }

        DeleteTarget = vault;
        DeletePassword = "";
        DeleteOverlayError = "";
        IsDeletePasswordVisible = false;
        IsDeletePasswordStep = false;
        IsDeleteOverlayOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteOverlay()
    {
        if (IsBusy)
            return;

        ClearDeleteOverlay();
    }

    [RelayCommand]
    private void ContinueDeleteOverlay()
    {
        DeleteOverlayError = "";
        DeletePassword = "";
        IsDeletePasswordVisible = false;
        IsDeletePasswordStep = true;
    }

    [RelayCommand]
    private void ToggleDeletePasswordVisibility()
    {
        IsDeletePasswordVisible = !IsDeletePasswordVisible;
    }

    [RelayCommand]
    private async Task ConfirmDeleteOverlayAsync()
    {
        Error = "";
        DeleteOverlayError = "";

        var vault = DeleteTarget;
        if (vault is null)
        {
            ClearDeleteOverlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(DeletePassword))
        {
            DeleteOverlayError = "Enter the master password to continue.";
            return;
        }

        IsBusy = true;
        try
        {
            var deletePath = VaultFileGuard.EnsureSafeVaultDeletionTarget(vault.VaultPath);
            var unlockResult = await _vaultService.UnlockAsync(deletePath, DeletePassword);
            if (!unlockResult.Success)
            {
                DeleteOverlayError = unlockResult.Error ?? "Wrong master password.";
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKey)
                Array.Clear(vaultKey, 0, vaultKey.Length);

            SqliteConnection.ClearAllPools();

            await _root.ClearClipboardAsync();
            DeleteSidecarIfExists(deletePath, "-wal");
            DeleteSidecarIfExists(deletePath, "-shm");
            DeleteSidecarIfExists(deletePath, "-journal");
            File.Delete(deletePath);

            if (!_vaultRegistry.RemoveVault(deletePath))
            {
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, deletePath, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            ClearDeleteOverlay();
            ReloadVaults();
            Status = $"{vault.DisplayLabel} was deleted permanently.";
            _root.LogActivity("vault", "Vault deleted", $"Permanently deleted {vault.DisplayLabel}.", "danger", vault.VaultPath, vault.DisplayLabel);
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

    [RelayCommand]
    private async Task EditVaultAsync(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        try
        {
            var (confirmed, displayName, description) = await _root.ShowEditVaultDialogAsync(
                vault.DisplayName,
                vault.Description,
                vault.VaultPath);

            if (!confirmed)
                return;

            _vaultRegistry.UpsertVault(
                vault.VaultPath,
                displayName,
                description,
                vault.IsDefault);

            ReloadVaults(vault.VaultPath);
            Status = "Vault metadata saved.";
            _root.LogActivity("vault", "Vault metadata updated", $"Updated metadata for {displayName}.", "info", vault.VaultPath, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void MakeDefault(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        try
        {
            _vaultRegistry.SetDefaultVault(vault.VaultPath);
            ReloadVaults(vault.VaultPath);
            Status = "Default vault updated.";
            _root.LogActivity("vault", "Default vault changed", $"Marked {vault.DisplayLabel} as the default vault.", "info", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    partial void OnSelectedVaultChanged(VaultRecordVm? value)
    {
        if (value is null)
        {
            Status = Vaults.Count == 0
                ? "No vaults are registered yet. Create your first vault to continue."
                : "Select a vault to unlock.";
            return;
        }

        Status = value.Exists
            ? $"Selected {value.DisplayLabel}."
            : "Selected vault file is missing.";
    }

    private void ReloadVaults(string? selectPath = null)
    {
        IsBusy = true;
        Error = "";

        try
        {
            var registry = _vaultRegistry.Load();
            var selectedPath = NormalizePath(selectPath ?? _root.VaultPath);

            var vaults = registry.Vaults.Select(x => new VaultRecordVm(x)).ToArray();

            _allVaults.Clear();
            _allVaults.AddRange(vaults);

            RecentVaults.Clear();
            foreach (var vault in _vaultRegistry.ListRecentVaults())
                RecentVaults.Add(new VaultRecordVm(vault));

            ApplyFilters();

            SelectedVault = Vaults.FirstOrDefault(x => string.Equals(NormalizePath(x.VaultPath), selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? Vaults.FirstOrDefault(x => x.IsDefault)
                ?? Vaults.FirstOrDefault();

            OnPropertyChanged(nameof(VaultCount));
            OnPropertyChanged(nameof(ExistingVaultCount));
            OnPropertyChanged(nameof(TotalStorageDisplay));
            if (vaults.Length == 0)
            {
                Status = "No vaults are registered yet. Create your first vault to continue.";
            }
            else if (SelectedVault is not null)
            {
                Status = SelectedVault.Exists
                    ? $"Loaded {vaults.Length} vault{(vaults.Length == 1 ? "" : "s")}."
                    : "Selected vault file is missing.";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = "Could not load the vault list.";
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
            "name" => items.OrderBy(vault => vault.DisplayLabel, StringComparer.OrdinalIgnoreCase),
            _ => items
                .OrderByDescending(vault => DateTimeOffset.TryParse(vault.LastOpenedAtUtc, out var opened) ? opened : DateTimeOffset.MinValue)
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

    private void ClearDeleteOverlay()
    {
        IsDeleteOverlayOpen = false;
        IsDeletePasswordStep = false;
        IsDeletePasswordVisible = false;
        DeleteOverlayError = "";
        DeletePassword = "";
        DeleteTarget = null;
    }

    private static void CopySidecarIfExists(string sourcePath, string targetPath, string suffix)
    {
        var source = sourcePath + suffix;
        if (!File.Exists(source))
            return;

        File.Copy(source, targetPath + suffix, overwrite: false);
    }

    private static void DeleteSidecarIfExists(string vaultPath, string suffix)
    {
        var sidecar = vaultPath + suffix;
        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }

    private static long GetVaultSize(string path)
        => File.Exists(path) ? new FileInfo(path).Length : 0L;

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        decimal display = bytes;
        var unitIndex = 0;

        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return $"{display:0.##} {units[unitIndex]}";
    }
}

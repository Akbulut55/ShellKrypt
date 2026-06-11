using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel : ViewModelBase
{
    private const int VaultPageSize = 3;
    private readonly MainWindowViewModel _root;
    private readonly VaultRegistryService _vaultRegistry;
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly List<VaultRecordVm> _allVaults = new();
    private SecurityAcknowledgementAction _pendingSecurityAcknowledgementAction;
    private VaultRecordVm? _pendingSecurityAcknowledgementVault;
    private int _filteredVaultCount;

    public ObservableCollection<VaultRecordVm> Vaults { get; } = new();
    public ObservableCollection<VaultRecordVm> RecentVaults { get; } = new();

    [ObservableProperty] private VaultRecordVm? selectedVault;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeSort = "recent";
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private string status = "";
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
    [ObservableProperty] private bool isSecurityAcknowledgementOpen;
    [ObservableProperty] private bool securityAcknowledgementConfirmed;

    public WelcomeViewModel(MainWindowViewModel root, VaultRegistryService vaultRegistry)
    {
        _root = root;
        _vaultRegistry = vaultRegistry;
        Status = T(_root, "Welcome.Status.SelectVaultOrCreate");
        ReloadVaults();
    }

    public bool IsRecentSortActive => ActiveSort == "recent";
    public bool IsNameSortActive => ActiveSort == "name";
    public bool HasVaults => Vaults.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasDeleteOverlayError => !string.IsNullOrWhiteSpace(DeleteOverlayError);
    public int VaultCount => _allVaults.Count;
    public int ExistingVaultCount => _allVaults.Count(vault => vault.Exists);
    public string ExistingVaultCountDisplay => T(_root, ExistingVaultCount == 1 ? "Welcome.Stats.AvailableVaultOne" : "Welcome.Stats.AvailableVaultMany", ExistingVaultCount);
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredVaultCount / (double)VaultPageSize));
    public bool HasMultiplePages => TotalPages > 1;
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public string PageIndicator => $"{CurrentPage} / {TotalPages}";
    public string TotalStorageDisplay => FormatBytes(_allVaults.Where(vault => vault.Exists).Sum(vault => GetVaultSize(vault.VaultPath)));
    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Welcome.Empty.NoVaultsTitle")
        : T(_root, "Welcome.Empty.NoSearchTitle");
    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Welcome.Empty.NoVaultsSubtitle")
        : T(_root, "Welcome.Empty.NoSearchSubtitle");
    public bool IsDeleteWarningStep => IsDeleteOverlayOpen && !IsDeletePasswordStep;
    public string DeleteWarningTitle => T(_root, "Welcome.Delete.Title", DeleteTarget?.DisplayLabel ?? T(_root, "Welcome.Vault.Label"));
    public string DeletePasswordTitle => T(_root, "Welcome.Delete.PasswordTitle");
    public string DeletePasswordDetail => DeleteTarget?.VaultPath ?? "";
    public string DeletePasswordVisibilityLabel => IsDeletePasswordVisible ? T(_root, "Common.Hide") : T(_root, "Common.Show");
    public string RemoveOverlayTitle => T(_root, "Welcome.Remove.Title", RemoveTarget?.DisplayLabel ?? T(_root, "Welcome.Vault.Label"));
    public string RemoveOverlayDetail => T(_root, "Welcome.Remove.Detail");
    public bool CanAcceptSecurityAcknowledgement => SecurityAcknowledgementConfirmed;

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnDeleteOverlayErrorChanged(string value) => OnPropertyChanged(nameof(HasDeleteOverlayError));
    partial void OnSecurityAcknowledgementConfirmedChanged(bool value) => AcceptSecurityAcknowledgementCommand.NotifyCanExecuteChanged();

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

    public override void RefreshLocalization()
    {
        foreach (var vault in _allVaults)
            vault.RefreshLocalization();

        foreach (var vault in Vaults)
            vault.RefreshLocalization();

        foreach (var vault in RecentVaults)
            vault.RefreshLocalization();

        NotifyLocalized(
            nameof(EmptyStateTitle),
            nameof(EmptyStateSubtitle),
            nameof(DeleteWarningTitle),
            nameof(DeletePasswordTitle),
            nameof(DeletePasswordVisibilityLabel),
            nameof(RemoveOverlayTitle),
            nameof(RemoveOverlayDetail),
            nameof(ExistingVaultCountDisplay));

        OnSelectedVaultChanged(SelectedVault);
    }
}

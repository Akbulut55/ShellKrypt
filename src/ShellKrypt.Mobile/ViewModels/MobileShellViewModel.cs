using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Items;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using ShellKrypt.Mobile.Security;
using ShellKrypt.Mobile.Storage;
using ShellKrypt.UI.Shared.Navigation;
using ShellKrypt.UI.Shared.Search;

namespace ShellKrypt.Mobile.ViewModels;

public sealed partial class MobileShellViewModel : ObservableObject
{
    private readonly IVaultService _vaultService;
    private readonly IVaultItemSummaryService _summaryService;
    private readonly string _vaultPath;
    private byte[]? _vaultKey;
    private readonly List<MobileListItemViewModel> _items = new();

    public MobileShellViewModel()
        : this(
            new SqliteVaultService(),
            new VaultItemSummaryService(new SqliteItemRepository(), new VaultItemPayloadReader()),
            GetDefaultMobileVaultPath())
    {
    }

    public MobileShellViewModel(
        IVaultService vaultService,
        IVaultItemSummaryService summaryService,
        string vaultPath)
    {
        _vaultService = vaultService;
        _summaryService = summaryService;
        _vaultPath = vaultPath;

        Sections = new ObservableCollection<MobileNavItem>(
            ShellKryptSectionCatalog.MobileSections.Select(section => new MobileNavItem(section)));

        SelectedSection = Sections.First();
        SelectedSection.IsSelected = true;
        Status = HasLocalVault
            ? "Local mobile vault found. Unlock with your master password."
            : "Create a local mobile vault on this device.";
    }

    public ObservableCollection<MobileNavItem> Sections { get; }

    public string AppTitle => "ShellKrypt";
    public string VaultPath => _vaultPath;
    public bool HasLocalVault => File.Exists(_vaultPath);
    public MobileVaultStoragePolicy VaultStoragePolicy { get; } = MobileVaultStoragePolicy.Default;

    [ObservableProperty] private MobilePageMode pageMode = MobilePageMode.Welcome;
    [ObservableProperty] private MobileNavItem? selectedSection;
    [ObservableProperty] private MobileListItemViewModel? selectedItem;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string masterPassword = string.Empty;
    [ObservableProperty] private string confirmMasterPassword = string.Empty;
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string error = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool lockOnBackground = MobileSecuritySettings.Default.LockOnBackground;
    [ObservableProperty] private bool enablePrivacyScreen = MobileSecuritySettings.Default.EnablePrivacyScreen;
    [ObservableProperty] private bool warnBeforeCopy = MobileSecuritySettings.Default.WarnBeforeCopy;
    [ObservableProperty] private bool allowBiometricUnlock = MobileSecuritySettings.Default.AllowBiometricUnlock;
    [ObservableProperty] private int clipboardClearSeconds = MobileSecuritySettings.DefaultClipboardClearSeconds;

    public bool IsWelcomeVisible => PageMode == MobilePageMode.Welcome;
    public bool IsCreateVisible => PageMode == MobilePageMode.Create;
    public bool IsUnlockVisible => PageMode == MobilePageMode.Unlock;
    public bool IsMainVisible => PageMode == MobilePageMode.Main;
    public bool IsListVisible => IsMainVisible && SelectedSection?.Key != ShellKryptSectionKeys.Settings;
    public bool IsSettingsVisible => IsMainVisible && SelectedSection?.Key == ShellKryptSectionKeys.Settings;
    public bool IsDetailVisible => PageMode == MobilePageMode.Detail;
    public bool IsEditVisible => PageMode == MobilePageMode.Edit;
    public bool IsBackupVisible => PageMode == MobilePageMode.Backup;
    public bool ShowBottomNavigation => PageMode == MobilePageMode.Main;
    public bool ShowBackButton => PageMode is MobilePageMode.Create or MobilePageMode.Unlock or MobilePageMode.Detail or MobilePageMode.Edit or MobilePageMode.Backup;
    public bool ShowLockButton => PageMode == MobilePageMode.Main;
    public bool ShowSearch => IsListVisible;
    public bool ShowAddAction => false;
    public bool ShowEditAction => false;
    public bool HasSelectedItem => SelectedItem is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasCurrentItems => CurrentItems.Any();
    public bool ShowListEmptyState => IsListVisible && !HasCurrentItems;
    public bool ShowUnsupportedAddNotice => IsListVisible && SelectedSection?.SupportsAdd == true;

    public string PageTitle => PageMode switch
    {
        MobilePageMode.Welcome => "ShellKrypt",
        MobilePageMode.Create => "Create Vault",
        MobilePageMode.Unlock => "Unlock Vault",
        MobilePageMode.Detail => SelectedItem?.Title ?? "Details",
        MobilePageMode.Edit => SelectedItem is null ? $"New {CurrentSingularLabel}" : $"Edit {CurrentSingularLabel}",
        MobilePageMode.Backup => "Backup & Export",
        _ => SelectedSection?.Title ?? "All Items"
    };

    public string PageSubtitle => PageMode switch
    {
        MobilePageMode.Welcome => "Local-only encrypted vault for mobile.",
        MobilePageMode.Create => "Create an app-private .skvault on this device. There is no password recovery.",
        MobilePageMode.Unlock => "Open the local mobile vault with your master password.",
        MobilePageMode.Detail => SelectedItem?.Subtitle ?? "Review this encrypted item.",
        MobilePageMode.Edit => "Mobile add/edit pages are intentionally disabled until each flow is implemented.",
        MobilePageMode.Backup => "Guided backup, restore, and plaintext export warnings.",
        _ => SelectedSection?.Subtitle ?? "Local encrypted vault workspace."
    };

    public string CurrentSingularLabel => SelectedSection?.Key switch
    {
        ShellKryptSectionKeys.WebLogins => "login",
        ShellKryptSectionKeys.Cards => "card",
        ShellKryptSectionKeys.ApiKeys => "API key",
        ShellKryptSectionKeys.Authenticator => "authenticator",
        ShellKryptSectionKeys.Notes => "note",
        _ => "item"
    };

    public string SearchPlaceholder => SelectedSection?.Key switch
    {
        ShellKryptSectionKeys.WebLogins => "Search title or website...",
        ShellKryptSectionKeys.Cards => "Search card or bank...",
        ShellKryptSectionKeys.ApiKeys => "Search key or provider...",
        ShellKryptSectionKeys.Authenticator => "Search issuer or account...",
        ShellKryptSectionKeys.Notes => "Search notes...",
        ShellKryptSectionKeys.Audit => "Search findings...",
        ShellKryptSectionKeys.Activity => "Search activity...",
        _ => "Search all items..."
    };

    public string EmptyStateTitle => SelectedSection?.Key switch
    {
        ShellKryptSectionKeys.Vault => "No items in this vault yet",
        ShellKryptSectionKeys.WebLogins => "No web logins yet",
        ShellKryptSectionKeys.Cards => "No credit cards yet",
        ShellKryptSectionKeys.ApiKeys => "No API keys yet",
        ShellKryptSectionKeys.Authenticator => "No authenticator entries yet",
        ShellKryptSectionKeys.Notes => "No markdown notes yet",
        ShellKryptSectionKeys.Audit => "Security audit is not enabled on mobile yet",
        ShellKryptSectionKeys.Activity => "Activity logs are not enabled on mobile yet",
        _ => "No items"
    };

    public string EmptyStateSubtitle => SelectedSection?.SupportsAdd == true
        ? "Add/edit mobile pages are the next milestone. Existing vault items still appear here after unlock."
        : "This mobile section will be connected after the core vault flows are stable.";

    public IEnumerable<MobileListItemViewModel> CurrentItems
    {
        get
        {
            IEnumerable<MobileListItemViewModel> items = _items;
            var key = SelectedSection?.Key ?? ShellKryptSectionKeys.Vault;

            if (key != ShellKryptSectionKeys.Vault)
                items = items.Where(item => string.Equals(item.SectionKey, key, StringComparison.Ordinal));

            return items.Where(item => ItemSearchMatcher.Matches(
                SearchText,
                item.Title,
                item.Subtitle,
                item.Meta,
                item.Badge));
        }
    }

    public MobileSecuritySettings CurrentSecuritySettings => new MobileSecuritySettings(
        LockOnBackground,
        EnablePrivacyScreen,
        WarnBeforeCopy,
        ClipboardClearSeconds,
        AllowBiometricUnlock).Normalize();

    public string ClipboardSummary =>
        $"Copied secrets clear after {CurrentSecuritySettings.ClipboardClearSeconds} seconds. {CurrentSecuritySettings.ClipboardBoundaryText}";

    public string BiometricSummary => CurrentSecuritySettings.BiometricBoundaryText;

    [RelayCommand]
    private void ShowUnlock()
    {
        Error = string.Empty;
        Status = HasLocalVault
            ? "Enter the master password for the local mobile vault."
            : "No local mobile vault exists on this device yet.";
        PageMode = HasLocalVault ? MobilePageMode.Unlock : MobilePageMode.Create;
    }

    [RelayCommand]
    private void CreateVault()
    {
        Error = string.Empty;
        MasterPassword = string.Empty;
        ConfirmMasterPassword = string.Empty;
        PageMode = MobilePageMode.Create;
    }

    [RelayCommand]
    private async Task SaveNewVaultAsync()
    {
        if (IsBusy)
            return;

        Error = string.Empty;
        Status = string.Empty;

        if (HasLocalVault)
        {
            Error = "A local mobile vault already exists. Unlock it instead.";
            PageMode = MobilePageMode.Unlock;
            return;
        }

        if (!string.Equals(MasterPassword, ConfirmMasterPassword, StringComparison.Ordinal))
        {
            Error = "Master password confirmation does not match.";
            return;
        }

        IsBusy = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_vaultPath)!);
            await _vaultService.CreateAsync(_vaultPath, MasterPassword);
            Status = "Vault created on this device.";
            await UnlockVaultAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            ConfirmMasterPassword = string.Empty;
            IsBusy = false;
            OnPropertyChanged(nameof(HasLocalVault));
        }
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (IsBusy)
            return;

        Error = string.Empty;
        Status = string.Empty;

        if (!HasLocalVault)
        {
            Error = "Create a local mobile vault before unlocking.";
            PageMode = MobilePageMode.Create;
            return;
        }

        IsBusy = true;
        try
        {
            await UnlockVaultAsync();
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
    private void Lock()
    {
        if (_vaultKey is { Length: > 0 })
            CryptographicOperations.ZeroMemory(_vaultKey);

        _vaultKey = null;
        _items.Clear();
        SelectedItem = null;
        SearchText = string.Empty;
        MasterPassword = string.Empty;
        Status = "Vault locked.";
        PageMode = MobilePageMode.Unlock;
        NotifyListProperties();
        NotifyPageProperties();
    }

    [RelayCommand]
    private void SelectSection(MobileNavItem? section)
    {
        if (section is null)
            return;

        foreach (var item in Sections)
            item.IsSelected = ReferenceEquals(item, section);

        SelectedSection = section;
        SelectedItem = null;
        SearchText = string.Empty;
        PageMode = MobilePageMode.Main;
        NotifyPageProperties();
        NotifyListProperties();
    }

    [RelayCommand]
    private void OpenDetail(MobileListItemViewModel? item)
    {
        if (item is null)
            return;

        SelectedItem = item;
        PageMode = MobilePageMode.Detail;
    }

    [RelayCommand]
    private void StartAdd()
    {
        Error = "Mobile add/edit pages are not implemented yet.";
    }

    [RelayCommand]
    private void StartEdit()
    {
        Error = "Mobile add/edit pages are not implemented yet.";
    }

    [RelayCommand]
    private void OpenBackupFlow() => PageMode = MobilePageMode.Backup;

    [RelayCommand]
    private void GoBack()
    {
        Error = string.Empty;

        if (PageMode is MobilePageMode.Create or MobilePageMode.Unlock)
        {
            PageMode = MobilePageMode.Welcome;
            return;
        }

        if (PageMode is MobilePageMode.Detail or MobilePageMode.Edit or MobilePageMode.Backup)
        {
            PageMode = MobilePageMode.Main;
            return;
        }
    }

    private async Task UnlockVaultAsync()
    {
        var result = await _vaultService.UnlockAsync(_vaultPath, MasterPassword);
        if (!result.Success)
        {
            Error = result.Error ?? "Unlock failed.";
            return;
        }

        _vaultKey = result.VaultKey!;
        MasterPassword = string.Empty;
        await LoadItemsAsync();
        PageMode = MobilePageMode.Main;
        SelectSection(Sections.First());
        Status = "Vault unlocked.";
    }

    private async Task LoadItemsAsync()
    {
        if (_vaultKey is null)
            return;

        var result = await _summaryService.ListAsync(
            _vaultPath,
            _vaultKey,
            ItemListQuery.Default(pageSize: 10_000));

        _items.Clear();
        _items.AddRange(result.AllItems.Select(ToMobileItem));
        NotifyListProperties();
    }

    partial void OnPageModeChanged(MobilePageMode value) => NotifyPageProperties();

    partial void OnSelectedSectionChanged(MobileNavItem? value)
    {
        NotifyPageProperties();
        NotifyListProperties();
    }

    partial void OnSelectedItemChanged(MobileListItemViewModel? value) => NotifyPageProperties();

    partial void OnSearchTextChanged(string value) => NotifyListProperties();

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnLockOnBackgroundChanged(bool value) => NotifySecurityProperties();

    partial void OnEnablePrivacyScreenChanged(bool value) => NotifySecurityProperties();

    partial void OnWarnBeforeCopyChanged(bool value) => NotifySecurityProperties();

    partial void OnAllowBiometricUnlockChanged(bool value) => NotifySecurityProperties();

    partial void OnClipboardClearSecondsChanged(int value)
    {
        if (value < MobileSecuritySettings.MinimumClipboardClearSeconds)
        {
            ClipboardClearSeconds = MobileSecuritySettings.MinimumClipboardClearSeconds;
            return;
        }

        NotifySecurityProperties();
    }

    private void NotifyPageProperties()
    {
        OnPropertyChanged(nameof(IsWelcomeVisible));
        OnPropertyChanged(nameof(IsCreateVisible));
        OnPropertyChanged(nameof(IsUnlockVisible));
        OnPropertyChanged(nameof(IsMainVisible));
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsDetailVisible));
        OnPropertyChanged(nameof(IsEditVisible));
        OnPropertyChanged(nameof(IsBackupVisible));
        OnPropertyChanged(nameof(ShowBottomNavigation));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(ShowLockButton));
        OnPropertyChanged(nameof(ShowSearch));
        OnPropertyChanged(nameof(ShowAddAction));
        OnPropertyChanged(nameof(ShowEditAction));
        OnPropertyChanged(nameof(ShowListEmptyState));
        OnPropertyChanged(nameof(ShowUnsupportedAddNotice));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(CurrentSingularLabel));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private void NotifyListProperties()
    {
        OnPropertyChanged(nameof(CurrentItems));
        OnPropertyChanged(nameof(HasCurrentItems));
        OnPropertyChanged(nameof(ShowListEmptyState));
        OnPropertyChanged(nameof(ShowUnsupportedAddNotice));
    }

    private void NotifySecurityProperties()
    {
        OnPropertyChanged(nameof(CurrentSecuritySettings));
        OnPropertyChanged(nameof(ClipboardSummary));
        OnPropertyChanged(nameof(BiometricSummary));
    }

    private static MobileListItemViewModel ToMobileItem(VaultItemSummary summary)
        => new(
            Title: summary.Title,
            Subtitle: summary.Subtitle,
            Meta: string.IsNullOrWhiteSpace(summary.Identifier) ? FormatDate(summary.UpdatedAtUtc) : summary.Identifier,
            Badge: DisplayTypeLabel(summary.Type),
            SensitiveHint: SensitiveHint(summary.Type),
            Id: summary.Id,
            SectionKey: SectionKey(summary.Type));

    private static string SectionKey(ItemType type)
        => type switch
        {
            ItemType.Web => ShellKryptSectionKeys.WebLogins,
            ItemType.Card => ShellKryptSectionKeys.Cards,
            ItemType.Note => ShellKryptSectionKeys.Notes,
            ItemType.Authenticator => ShellKryptSectionKeys.Authenticator,
            ItemType.ApiKey => ShellKryptSectionKeys.ApiKeys,
            _ => ShellKryptSectionKeys.Vault
        };

    private static string DisplayTypeLabel(ItemType type)
        => type switch
        {
            ItemType.Web => "Login",
            ItemType.Card => "Card",
            ItemType.Note => "Note",
            ItemType.Authenticator => "2FA",
            ItemType.ApiKey => "API",
            _ => "Item"
        };

    private static string SensitiveHint(ItemType type)
        => type switch
        {
            ItemType.Web => "Password remains encrypted and hidden in this list.",
            ItemType.Card => "Card number and CVC remain hidden in this list.",
            ItemType.ApiKey => "Secret fields remain masked until details support is added.",
            ItemType.Authenticator => "OTP seed remains hidden in this list.",
            ItemType.Note => "Note content is summarized only.",
            _ => string.Empty
        };

    private static string FormatDate(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture)
            : "Updated";

    private static string GetDefaultMobileVaultPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;

        return Path.Combine(basePath, "ShellKrypt", "MobileVault.skvault");
    }
}

public sealed partial class MobileNavItem : ObservableObject
{
    public MobileNavItem(ShellKryptSectionDescriptor descriptor)
    {
        Key = descriptor.Key;
        Title = descriptor.Title;
        ShortTitle = descriptor.ShortTitle;
        Glyph = descriptor.Glyph;
        Subtitle = descriptor.Subtitle;
        SupportsAdd = descriptor.SupportsAdd;
    }

    public string Key { get; }
    public string Title { get; }
    public string ShortTitle { get; }
    public string Glyph { get; }
    public string Subtitle { get; }
    public bool SupportsAdd { get; }

    [ObservableProperty] private bool isSelected;
}

public sealed record MobileListItemViewModel(
    string Title,
    string Subtitle,
    string Meta,
    string Badge,
    string SensitiveHint = "",
    string Id = "",
    string SectionKey = ShellKryptSectionKeys.Vault);

public enum MobilePageMode
{
    Welcome,
    Create,
    Unlock,
    Main,
    Detail,
    Edit,
    Backup
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Tools;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Tools;
using ShellKrypt.Infrastructure.Vaulting;
using ShellKrypt.Desktop.Views;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly VaultRegistryStore _vaultRegistryStore = new();
    private readonly ActivityLogStore _activityLogStore = new();
    private readonly ClipboardService _clipboardService = new();
    private readonly AuthenticatorQrImportService _authenticatorQrImportService = new();
    private readonly SessionSecurityService _sessionSecurity;
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly IItemRepository _itemRepo = new SqliteItemRepository();
    private readonly IWebLoginService _webLoginService;
    private readonly ICardService _cardService;
    private readonly INoteService _noteService;
    private readonly IAuthenticatorService _authenticatorService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IHealthAuditService _healthAuditService;
    private readonly ICryptoToolsService _cryptoToolsService = new CryptoToolsService();

    public event EventHandler? ActivityChanged;

    [ObservableProperty]
    private ViewModelBase current = null!;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private int lockOnDeactivateSeconds;
    [ObservableProperty] private int clipboardClearSeconds;
    [ObservableProperty] private bool clipboardCopyEnabled;
    [ObservableProperty] private AppThemeMode themeMode;

    public MainWindowViewModel()
    {
        _sessionSecurity = new SessionSecurityService(Lock);
        _webLoginService = new WebLoginService(_itemRepo);
        _cardService = new CardService(_itemRepo);
        _noteService = new NoteService(_itemRepo);
        _authenticatorService = new AuthenticatorService(_itemRepo);
        _apiKeyService = new ApiKeyService(_itemRepo);
        _healthAuditService = new HealthAuditService(_itemRepo);

        var settings = _settingsStore.Load();
        var sessionSecurity = settings.ToSessionSecuritySettings();
        autoLockEnabled = sessionSecurity.AutoLockEnabled;
        autoLockMinutes = sessionSecurity.AutoLockMinutes;
        lockOnDeactivate = sessionSecurity.LockOnDeactivate;
        lockOnDeactivateSeconds = sessionSecurity.LockOnDeactivateSeconds;
        clipboardClearSeconds = sessionSecurity.ClipboardClearSeconds;
        clipboardCopyEnabled = sessionSecurity.ClipboardCopyEnabled;
        themeMode = settings.ThemeMode;

        _sessionSecurity.ApplySettings(sessionSecurity);

        Current = new WelcomeViewModel(this, _vaultRegistryStore);
        ApplyTheme(themeMode);
    }

    public string? VaultPath => _state.VaultPath;
    public byte[] VaultKey => _state.GetVaultKeyOrThrow();
    public bool IsUnlocked => _state.VaultKey is not null;
    public string VaultPathDisplay => VaultPath ?? "(no vault selected)";
    public ActivityLogStore ActivityLogStore => _activityLogStore;

    public void SetVaultPath(string path)
    {
        var nextPath = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
        if (!string.Equals(_state.VaultPath, nextPath, StringComparison.OrdinalIgnoreCase))
            _ = _clipboardService.ClearAsync();

        _state.VaultPath = nextPath;
    }
    public void AttachClipboard(IClipboard? clipboard) => _clipboardService.Attach(clipboard);

    public void RecordActivity() => _sessionSecurity.RecordActivity();

    public void HandleWindowActivated() => _sessionSecurity.HandleWindowActivated();

    public void HandleWindowDeactivated() => _sessionSecurity.HandleWindowDeactivated();

    public void GoWelcome() => Current = new WelcomeViewModel(this, _vaultRegistryStore);
    public void GoCreateVault() => Current = new CreateVaultViewModel(this, _vaultService, _vaultRegistryStore);
    public void GoUnlock() => Current = new UnlockViewModel(this, _vaultService, _vaultRegistryStore);

    public void OnUnlocked(byte[] vaultKey)
    {
        _state.VaultKey = vaultKey;

        if (!string.IsNullOrWhiteSpace(_state.VaultPath))
            _vaultRegistryStore.MarkOpened(_state.VaultPath);

        LogActivity(
            category: "vault",
            title: "Vault unlocked",
            detail: $"Opened {GetVaultDisplayName(_state.VaultPath)}.",
            severity: "success",
            vaultPath: _state.VaultPath,
            affectedItem: GetVaultDisplayName(_state.VaultPath));

        _sessionSecurity.SetUnlocked(true);
        Current = new ShellViewModel(this, _itemRepo, _webLoginService, _cardService, _noteService, _authenticatorService, _apiKeyService, _authenticatorQrImportService, _healthAuditService, _cryptoToolsService, _activityLogStore);
    }

    public void Lock()
    {
        var vaultPath = _state.VaultPath;
        if (!string.IsNullOrWhiteSpace(vaultPath))
        {
            LogActivity(
                category: "vault",
                title: "Vault locked",
                detail: $"Locked {GetVaultDisplayName(vaultPath)}.",
                severity: "info",
                vaultPath: vaultPath,
                affectedItem: GetVaultDisplayName(vaultPath));
        }

        _sessionSecurity.SetUnlocked(false);
        _ = _clipboardService.ClearAsync();
        _state.ClearSensitive();
        GoWelcome();
    }

    public void ReloadShell()
    {
        if (!IsUnlocked)
            return;

        Current = new ShellViewModel(this, _itemRepo, _webLoginService, _cardService, _noteService, _authenticatorService, _apiKeyService, _authenticatorQrImportService, _healthAuditService, _cryptoToolsService, _activityLogStore);
        _sessionSecurity.RecordActivity();
    }

    public async Task CopyToClipboardAsync(string text)
    {
        if (!_sessionSecurity.Settings.ClipboardCopyEnabled)
            return;

        await _clipboardService.CopyAsync(text, _sessionSecurity.ClipboardClearDelay);
    }

    public async Task ClearClipboardAsync()
    {
        await _clipboardService.ClearAsync();
    }

    public async Task<Bitmap?> TryGetClipboardBitmapAsync()
    {
        return await _clipboardService.TryGetBitmapAsync();
    }

    public async Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(fileTypeName)
                {
                    Patterns = extensions.Select(ToPattern).ToArray()
                }
            ]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedName) ? "file" : suggestedName,
            DefaultExtension = defaultExtension.TrimStart('.'),
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(fileTypeName)
                {
                    Patterns = extensions.Select(ToPattern).ToArray()
                }
            ]
        });

        return file?.TryGetLocalPath();
    }

    public async Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return false;

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new ConfirmActionWindow(title, message, detail, confirmText);
        return await dialog.ShowDialog<bool>(mainWindow);
    }

    public async Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return null;

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new PasswordPromptWindow(title, message, detail, confirmText);
        return await dialog.ShowDialog<string?>(mainWindow);
    }

    public async Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return (false, "", "");

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new ImportVaultWindow(initialPath, initialDisplayName);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.VaultPath, dialog.DisplayName);
    }

    public async Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return (false, displayName, description);

        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new EditVaultWindow(displayName, description, vaultPath);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.DisplayName, dialog.Description);
    }

    public void LogActivity(
        string category,
        string title,
        string detail,
        string severity = "info",
        string? vaultPath = null,
        string? affectedItem = null)
    {
        var targetVaultPath = string.IsNullOrWhiteSpace(vaultPath) ? VaultPath : vaultPath;
        var entry = new ActivityLogEntry(
            Id: Guid.NewGuid().ToString("N"),
            TimestampUtc: DateTimeOffset.UtcNow.ToString("O"),
            Category: string.IsNullOrWhiteSpace(category) ? "system" : category.Trim().ToLowerInvariant(),
            Title: title.Trim(),
            Detail: detail.Trim(),
            Severity: string.IsNullOrWhiteSpace(severity) ? "info" : severity.Trim().ToLowerInvariant(),
            VaultPath: targetVaultPath)
        {
            AffectedItem = string.IsNullOrWhiteSpace(affectedItem) ? null : affectedItem.Trim()
        };

        try
        {
            _activityLogStore.Append(entry, _state.VaultKey);
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    partial void OnAutoLockEnabledChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnAutoLockMinutesChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnLockOnDeactivateChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnLockOnDeactivateSecondsChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnClipboardClearSecondsChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnClipboardCopyEnabledChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnThemeModeChanged(AppThemeMode value)
    {
        ApplyTheme(value);
        SaveSettingsAndSyncSessionSecurity();
    }

    private void SaveSettingsAndSyncSessionSecurity()
    {
        try
        {
            var appSettings = new AppSettings
            {
                ThemeMode = ThemeMode
            };
            appSettings.ApplySessionSecuritySettings(BuildSessionSecuritySettings());
            _settingsStore.Save(appSettings);
        }
        catch
        {
        }

        _sessionSecurity.ApplySettings(BuildSessionSecuritySettings());
    }

    private static void ApplyTheme(AppThemeMode mode)
    {
        if (Application.Current is App app)
            app.ApplyTheme(mode);
    }

    private static string ToPattern(string extension)
    {
        var normalized = extension?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
            return "*.*";

        return normalized.StartsWith(".", StringComparison.Ordinal) ? $"*{normalized}" : $"*.{normalized.TrimStart('*')}";
    }

    private static string GetVaultDisplayName(string? vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
            return "Vault";

        return Path.GetFileNameWithoutExtension(vaultPath);
    }

    private SessionSecuritySettings BuildSessionSecuritySettings()
    {
        return new SessionSecuritySettings
        {
            AutoLockEnabled = AutoLockEnabled,
            AutoLockMinutes = AutoLockMinutes,
            LockOnDeactivate = LockOnDeactivate,
            LockOnDeactivateSeconds = LockOnDeactivateSeconds,
            ClipboardClearSeconds = ClipboardClearSeconds,
            ClipboardCopyEnabled = ClipboardCopyEnabled
        }.Normalize();
    }
}

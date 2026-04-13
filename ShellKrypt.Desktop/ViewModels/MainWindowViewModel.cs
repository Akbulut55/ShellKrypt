using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private readonly ClipboardService _clipboardService = new();
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly IItemRepository _itemRepo = new SqliteItemRepository();
    private readonly IWebLoginService _webLoginService;
    private readonly ICardService _cardService;
    private readonly ICryptoToolsService _cryptoToolsService = new CryptoToolsService();
    private readonly DispatcherTimer _autoLockTimer = new();
    private readonly DispatcherTimer _focusLossLockTimer = new() { Interval = TimeSpan.FromSeconds(20) };

    [ObservableProperty]
    private ViewModelBase current = null!;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private int lockOnDeactivateSeconds;
    [ObservableProperty] private int clipboardClearSeconds;
    [ObservableProperty] private AppThemeMode themeMode;

    public MainWindowViewModel()
    {
        _webLoginService = new WebLoginService(_itemRepo);
        _cardService = new CardService(_itemRepo);

        var settings = _settingsStore.Load();
        AutoLockEnabled = settings.AutoLockEnabled;
        AutoLockMinutes = Math.Max(1, settings.AutoLockMinutes);
        LockOnDeactivate = settings.LockOnDeactivate;
        LockOnDeactivateSeconds = Math.Max(1, settings.LockOnDeactivateSeconds);
        ClipboardClearSeconds = Math.Max(1, settings.ClipboardClearSeconds);
        themeMode = settings.ThemeMode;

        _autoLockTimer.Tick += (_, _) =>
        {
            if (IsUnlocked && AutoLockEnabled)
                Lock();
        };

        _focusLossLockTimer.Tick += (_, _) =>
        {
            StopFocusLossTimer();
            if (IsUnlocked && LockOnDeactivate)
                Lock();
        };

        Current = new WelcomeViewModel(this, _vaultRegistryStore);
        ApplyTheme(themeMode);
    }

    public string? VaultPath => _state.VaultPath;
    public byte[] VaultKey => _state.GetVaultKeyOrThrow();
    public bool IsUnlocked => _state.VaultKey is not null;
    public string VaultPathDisplay => VaultPath ?? "(no vault selected)";

    public void SetVaultPath(string path) => _state.VaultPath = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
    public void AttachClipboard(IClipboard? clipboard) => _clipboardService.Attach(clipboard);

    public void RecordActivity()
    {
        StopFocusLossTimer();

        if (!IsUnlocked || !AutoLockEnabled)
            return;

        RestartAutoLockTimer();
    }

    public void HandleWindowActivated()
    {
        StopFocusLossTimer();
        RecordActivity();
    }

    public void HandleWindowDeactivated()
    {
        if (IsUnlocked && LockOnDeactivate)
            RestartFocusLossTimer();
    }

    public void GoWelcome() => Current = new WelcomeViewModel(this, _vaultRegistryStore);
    public void GoCreateVault() => Current = new CreateVaultViewModel(this, _vaultService, _vaultRegistryStore);
    public void GoUnlock() => Current = new UnlockViewModel(this, _vaultService, _vaultRegistryStore);

    public void OnUnlocked(byte[] vaultKey)
    {
        _state.VaultKey = vaultKey;

        if (!string.IsNullOrWhiteSpace(_state.VaultPath))
            _vaultRegistryStore.MarkOpened(_state.VaultPath);

        Current = new ShellViewModel(this, _itemRepo, _webLoginService, _cardService, _cryptoToolsService);
        RestartAutoLockTimer();
    }

    public void Lock()
    {
        StopAutoLockTimer();
        StopFocusLossTimer();
        _ = _clipboardService.ClearAsync();
        _state.ClearSensitive();
        GoWelcome();
    }

    public void ReloadShell()
    {
        if (!IsUnlocked)
            return;

        Current = new ShellViewModel(this, _itemRepo, _webLoginService, _cardService, _cryptoToolsService);
        RestartAutoLockTimer();
    }

    public async Task CopyToClipboardAsync(string text)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, ClipboardClearSeconds));
        await _clipboardService.CopyAsync(text, delay);
    }

    public async Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

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

        var dialog = new ConfirmActionWindow(title, message, detail, confirmText);
        return await dialog.ShowDialog<bool>(mainWindow);
    }

    public async Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return null;

        var dialog = new PasswordPromptWindow(title, message, detail, confirmText);
        return await dialog.ShowDialog<string?>(mainWindow);
    }

    public async Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return (false, "", "");

        var dialog = new ImportVaultWindow(initialPath, initialDisplayName);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.VaultPath, dialog.DisplayName);
    }

    public async Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return (false, displayName, description);

        var dialog = new EditVaultWindow(displayName, description, vaultPath);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.DisplayName, dialog.Description);
    }

    partial void OnAutoLockEnabledChanged(bool value) => SaveSettingsAndUpdateTimer();
    partial void OnAutoLockMinutesChanged(int value) => SaveSettingsAndUpdateTimer();
    partial void OnLockOnDeactivateChanged(bool value) => SaveSettingsAndUpdateTimer();
    partial void OnLockOnDeactivateSecondsChanged(int value) => SaveSettingsAndUpdateTimer();
    partial void OnClipboardClearSecondsChanged(int value) => SaveSettingsAndUpdateTimer();
    partial void OnThemeModeChanged(AppThemeMode value)
    {
        ApplyTheme(value);
        SaveSettingsAndUpdateTimer();
    }

    private void SaveSettingsAndUpdateTimer()
    {
        try
        {
            _settingsStore.Save(new AppSettings
            {
                AutoLockEnabled = AutoLockEnabled,
                AutoLockMinutes = Math.Max(1, AutoLockMinutes),
                LockOnDeactivate = LockOnDeactivate,
                LockOnDeactivateSeconds = Math.Max(1, LockOnDeactivateSeconds),
                ClipboardClearSeconds = Math.Max(1, ClipboardClearSeconds),
                ThemeMode = ThemeMode,
            });
        }
        catch
        {
        }

        if (IsUnlocked)
            RestartAutoLockTimer();
    }

    private void RestartAutoLockTimer()
    {
        StopAutoLockTimer();

        if (!IsUnlocked || !AutoLockEnabled || AutoLockMinutes < 1)
            return;

        _autoLockTimer.Interval = TimeSpan.FromMinutes(AutoLockMinutes);
        _autoLockTimer.Start();
    }

    private void StopAutoLockTimer() => _autoLockTimer.Stop();

    private void RestartFocusLossTimer()
    {
        StopFocusLossTimer();
        _focusLossLockTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, LockOnDeactivateSeconds));
        _focusLossLockTimer.Start();
    }

    private void StopFocusLossTimer() => _focusLossLockTimer.Stop();

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
}

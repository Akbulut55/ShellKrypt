using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly VaultRegistryStore _vaultRegistryStore = new();
    private readonly ClipboardService _clipboardService = new();
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly IItemRepository _itemRepo = new SqliteItemRepository();
    private readonly DispatcherTimer _autoLockTimer = new();

    [ObservableProperty]
    private ViewModelBase current = null!;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private int clipboardClearSeconds;

    public MainWindowViewModel()
    {
        var settings = _settingsStore.Load();
        AutoLockEnabled = settings.AutoLockEnabled;
        AutoLockMinutes = Math.Max(1, settings.AutoLockMinutes);
        LockOnDeactivate = settings.LockOnDeactivate;
        ClipboardClearSeconds = Math.Max(1, settings.ClipboardClearSeconds);

        _autoLockTimer.Tick += (_, _) =>
        {
            if (IsUnlocked && AutoLockEnabled)
                Lock();
        };

        Current = new WelcomeViewModel(this, _vaultRegistryStore);
    }

    public string? VaultPath => _state.VaultPath;
    public byte[] VaultKey => _state.GetVaultKeyOrThrow();
    public bool IsUnlocked => _state.VaultKey is not null;
    public string VaultPathDisplay => VaultPath ?? "(no vault selected)";

    public void SetVaultPath(string path) => _state.VaultPath = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
    public void AttachClipboard(IClipboard? clipboard) => _clipboardService.Attach(clipboard);

    public void RecordActivity()
    {
        if (!IsUnlocked || !AutoLockEnabled)
            return;

        RestartAutoLockTimer();
    }

    public void HandleWindowActivated() => RecordActivity();

    public void HandleWindowDeactivated()
    {
        if (IsUnlocked && LockOnDeactivate)
            Lock();
    }

    public void GoWelcome() => Current = new WelcomeViewModel(this, _vaultRegistryStore);
    public void GoCreateVault() => Current = new CreateVaultViewModel(this, _vaultService, _vaultRegistryStore);
    public void GoUnlock() => Current = new UnlockViewModel(this, _vaultService, _vaultRegistryStore);

    public void OnUnlocked(byte[] vaultKey)
    {
        _state.VaultKey = vaultKey;

        if (!string.IsNullOrWhiteSpace(_state.VaultPath))
            _vaultRegistryStore.MarkOpened(_state.VaultPath);

        Current = new ShellViewModel(this, _itemRepo);
        RestartAutoLockTimer();
    }

    public void Lock()
    {
        StopAutoLockTimer();
        _ = _clipboardService.ClearAsync();
        _state.ClearSensitive();
        GoWelcome();
    }

    public async Task CopyToClipboardAsync(string text)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, ClipboardClearSeconds));
        await _clipboardService.CopyAsync(text, delay);
    }

    partial void OnAutoLockEnabledChanged(bool value) => SaveSettingsAndUpdateTimer();
    partial void OnAutoLockMinutesChanged(int value) => SaveSettingsAndUpdateTimer();
    partial void OnLockOnDeactivateChanged(bool value) => SaveSettingsAndUpdateTimer();
    partial void OnClipboardClearSecondsChanged(int value) => SaveSettingsAndUpdateTimer();

    private void SaveSettingsAndUpdateTimer()
    {
        try
        {
            _settingsStore.Save(new AppSettings
            {
                AutoLockEnabled = AutoLockEnabled,
                AutoLockMinutes = Math.Max(1, AutoLockMinutes),
                LockOnDeactivate = LockOnDeactivate,
                ClipboardClearSeconds = Math.Max(1, ClipboardClearSeconds),
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
}

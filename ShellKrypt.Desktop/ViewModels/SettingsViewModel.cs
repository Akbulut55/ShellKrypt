using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private string autoLockMinutesText = "";
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private string clipboardClearSecondsText = "";
    [ObservableProperty] private string status = "";

    public SettingsViewModel(MainWindowViewModel root)
    {
        _root = root;

        AutoLockEnabled = _root.AutoLockEnabled;
        AutoLockMinutesText = _root.AutoLockMinutes.ToString(CultureInfo.InvariantCulture);
        LockOnDeactivate = _root.LockOnDeactivate;
        ClipboardClearSecondsText = _root.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture);
        Status = "Settings save automatically.";
    }

    public string VaultPath => _root.VaultPathDisplay;
    public string SessionState => _root.IsUnlocked ? "Unlocked" : "Locked";

    partial void OnAutoLockEnabledChanged(bool value)
    {
        _root.AutoLockEnabled = value;
        OnPropertyChanged(nameof(SessionState));
    }

    partial void OnLockOnDeactivateChanged(bool value)
    {
        _root.LockOnDeactivate = value;
    }

    partial void OnAutoLockMinutesTextChanged(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            Status = "Auto-lock minutes must be a whole number.";
            return;
        }

        if (minutes < 1)
        {
            Status = "Auto-lock minutes must be at least 1.";
            return;
        }

        _root.AutoLockMinutes = minutes;
        Status = "Settings saved.";
    }

    partial void OnClipboardClearSecondsTextChanged(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            Status = "Clipboard timeout must be a whole number.";
            return;
        }

        if (seconds < 1)
        {
            Status = "Clipboard timeout must be at least 1 second.";
            return;
        }

        _root.ClipboardClearSeconds = seconds;
        Status = "Settings saved.";
    }

    [RelayCommand]
    private async Task CopyVaultPathAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            Status = "No vault path is available yet.";
            return;
        }

        try
        {
            await _root.CopyToClipboardAsync(_root.VaultPath);
            Status = $"Vault path copied. Clipboard clears in {_root.ClipboardClearSeconds} seconds.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}

using ShellKrypt.Application.Settings;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services.QuickFill;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class QuickFillController : IQuickFillController
{
    private readonly GlobalHotkeyService _hotkey;
    private readonly ForegroundWindowService _foregroundWindow;
    private readonly IDesktopSettingsController _settings;

    public QuickFillController(
        GlobalHotkeyService hotkey,
        ForegroundWindowService foregroundWindow,
        IDesktopSettingsController settings)
    {
        _hotkey = hotkey;
        _foregroundWindow = foregroundWindow;
        _settings = settings;
        _hotkey.HotkeyPressed += (_, _) => HotkeyPressed?.Invoke(this, EventArgs.Empty);
        _hotkey.StatusChanged += (_, _) => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? StatusChanged;
    public QuickFillSettings Settings => _settings.QuickFill;
    public string HotkeyStatus => _hotkey.Status;
    public bool CanConfigureSystemShortcut => _hotkey.CanConfigurePortalShortcut;

    public void Start() => _hotkey.Start(Settings);
    public void Stop() => _hotkey.Stop();

    public void ConfigureSystemShortcut()
    {
        _hotkey.ConfigurePortalShortcut();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveSettings()
    {
        _settings.SaveQuickFillSettings();
        Start();
    }

    public void AcceptAutoTypeAcknowledgement()
    {
        if (Settings.HasAutoTypeAcknowledgement)
            return;
        Settings.AutoTypeAcknowledgedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        SaveSettings();
    }

    public QuickFillTargetContext CaptureTarget() => _foregroundWindow.Capture();
}

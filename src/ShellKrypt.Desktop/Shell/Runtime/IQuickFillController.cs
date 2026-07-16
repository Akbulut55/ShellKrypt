using ShellKrypt.Application.Settings;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IQuickFillController
{
    event EventHandler? HotkeyPressed;
    event EventHandler? StatusChanged;
    QuickFillSettings Settings { get; }
    string HotkeyStatus { get; }
    bool CanConfigureSystemShortcut { get; }
    void Start();
    void Stop();
    void ConfigureSystemShortcut();
    void SaveSettings();
    void AcceptAutoTypeAcknowledgement();
    QuickFillTargetContext CaptureTarget();
}

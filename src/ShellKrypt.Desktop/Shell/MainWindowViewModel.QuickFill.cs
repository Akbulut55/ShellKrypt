using System;

namespace ShellKrypt.Desktop.Shell;

public partial class MainWindowViewModel
{
    public void AttachQuickFillHotkey()
    {
        _quickFillController.Start();
        if (Current is ShellViewModel shell)
            shell.QuickFill.RefreshHotkeyStatus();
    }

    public void Shutdown()
    {
        _quickFillController.Stop();
        Lock();
    }

    public void OpenQuickFillPopup()
    {
        var target = _quickFillController.CaptureTarget();
        var suppression = _sessionSecurity.SuppressTransientFocusLoss();
        _quickFillPopup.Open(_navigation, target, suppression);
    }
}

using Avalonia.Input.Platform;

namespace ShellKrypt.Desktop.Shell;

public partial class MainWindowViewModel
{
    public void AttachClipboard(IClipboard? clipboard) => _secureClipboard.Attach(clipboard);
}

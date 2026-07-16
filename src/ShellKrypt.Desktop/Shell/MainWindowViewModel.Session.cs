namespace ShellKrypt.Desktop.Shell;

public partial class MainWindowViewModel
{
    public void RecordActivity() => _sessionSecurity.RecordActivity();
    public void HandleWindowActivated() => _sessionSecurity.HandleWindowActivated();
    public void HandleWindowDeactivated() => _sessionSecurity.HandleWindowDeactivated();
    public void Lock() => _navigation.Lock();
}

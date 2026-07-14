using System;
using Avalonia.Threading;

namespace ShellKrypt.Desktop.Features.Authenticator;

public interface IAuthenticatorRefreshTimer
{
    event EventHandler? Tick;
    bool IsRunning { get; }
    void Start();
    void Stop();
}

public sealed class AuthenticatorRefreshTimer : IAuthenticatorRefreshTimer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public AuthenticatorRefreshTimer()
    {
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Tick;
    public bool IsRunning => _timer.IsEnabled;
    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
}

using System;
using Avalonia.Threading;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Services;

public sealed class SessionSecurityService
{
    private static readonly TimeSpan ActivityThrottle = TimeSpan.FromMilliseconds(750);

    private readonly DispatcherTimer _autoLockTimer = new();
    private readonly DispatcherTimer _focusLossLockTimer = new();
    private SessionSecuritySettings _settings = new();
    private bool _isUnlocked;
    private int _focusLossSuppressionDepth;
    private DateTimeOffset _lastActivityUtc = DateTimeOffset.MinValue;

    public SessionSecurityService()
    {
        _focusLossLockTimer.Interval = TimeSpan.FromSeconds(_settings.LockOnDeactivateSeconds);

        _autoLockTimer.Tick += (_, _) =>
        {
            StopAutoLockTimer();
            if (_isUnlocked && _settings.AutoLockEnabled)
                LockRequested?.Invoke(this, EventArgs.Empty);
        };

        _focusLossLockTimer.Tick += (_, _) =>
        {
            StopFocusLossTimer();
            if (_isUnlocked && _settings.LockOnDeactivate)
                LockRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? LockRequested;

    public SessionSecuritySettings Settings => _settings;
    public TimeSpan ClipboardClearDelay => TimeSpan.FromSeconds(_settings.ClipboardClearSeconds);

    public void ApplySettings(SessionSecuritySettings settings)
    {
        _settings = settings.Normalize();
        _focusLossLockTimer.Interval = TimeSpan.FromSeconds(_settings.LockOnDeactivateSeconds);

        if (!_isUnlocked)
        {
            StopAutoLockTimer();
            StopFocusLossTimer();
            return;
        }

        RestartAutoLockTimer();
        if (!_settings.LockOnDeactivate)
            StopFocusLossTimer();
        else if (_focusLossLockTimer.IsEnabled)
            RestartFocusLossTimer();
    }

    public void SetUnlocked(bool isUnlocked)
    {
        _isUnlocked = isUnlocked;
        _lastActivityUtc = DateTimeOffset.UtcNow;

        if (!_isUnlocked)
        {
            StopAutoLockTimer();
            StopFocusLossTimer();
            return;
        }

        RestartAutoLockTimer();
    }

    public void RecordActivity() => RecordActivity(force: false);

    public void HandleWindowActivated()
    {
        StopFocusLossTimer();
        RecordActivity(force: true);
    }

    public void HandleWindowDeactivated()
    {
        if (!_isUnlocked || !_settings.LockOnDeactivate || _focusLossSuppressionDepth > 0)
            return;

        RestartFocusLossTimer();
    }

    public IDisposable SuppressTransientFocusLoss()
    {
        _focusLossSuppressionDepth++;
        StopAutoLockTimer();
        StopFocusLossTimer();
        return new FocusLossSuppressionScope(this);
    }

    private void RecordActivity(bool force)
    {
        if (!_isUnlocked)
            return;

        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastActivityUtc < ActivityThrottle)
        {
            StopFocusLossTimer();
            return;
        }

        _lastActivityUtc = now;
        StopFocusLossTimer();
        RestartAutoLockTimer();
    }

    private void RestartAutoLockTimer()
    {
        StopAutoLockTimer();

        if (!_isUnlocked || !_settings.AutoLockEnabled || _settings.AutoLockMinutes < 1)
            return;

        _autoLockTimer.Interval = TimeSpan.FromMinutes(_settings.AutoLockMinutes);
        _autoLockTimer.Start();
    }

    private void RestartFocusLossTimer()
    {
        StopFocusLossTimer();

        if (!_isUnlocked || !_settings.LockOnDeactivate)
            return;

        _focusLossLockTimer.Start();
    }

    private void StopAutoLockTimer() => _autoLockTimer.Stop();

    private void StopFocusLossTimer() => _focusLossLockTimer.Stop();

    private void ReleaseFocusLossSuppression()
    {
        if (_focusLossSuppressionDepth > 0)
            _focusLossSuppressionDepth--;

        if (_focusLossSuppressionDepth == 0 && _isUnlocked)
            RestartAutoLockTimer();
    }

    private sealed class FocusLossSuppressionScope : IDisposable
    {
        private SessionSecurityService? _owner;

        public FocusLossSuppressionScope(SessionSecurityService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner?.ReleaseFocusLossSuppression();
            _owner = null;
        }
    }
}

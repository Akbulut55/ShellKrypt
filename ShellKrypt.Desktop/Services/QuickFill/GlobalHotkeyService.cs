using System;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Services.QuickFill;

public enum QuickFillPortalHotkeyState
{
    PortalUnavailable,
    PortalUnsupportedVersion,
    PortalSessionCreated,
    PortalShortcutListed,
    PortalShortcutBound,
    PortalNeedsConfiguration,
    PortalReady,
    PortalFailed
}

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly IQuickFillHotkeyBackend _backend = QuickFillHotkeyBackendSelector.Select();

    public GlobalHotkeyService()
    {
        _backend.HotkeyPressed += OnBackendHotkeyPressed;
        _backend.StatusChanged += OnBackendStatusChanged;
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? StatusChanged;

    public bool IsRegistered => _backend.IsRegistered;
    public string Status => _backend.Status;
    public QuickFillPortalHotkeyState PortalState => _backend.PortalState;
    public bool CanConfigurePortalShortcut => _backend.CanConfigurePortalShortcut;

    public void Start(QuickFillSettings settings) => _backend.Start(settings);
    public void Stop() => _backend.Stop();
    public void ConfigurePortalShortcut() => _backend.ConfigurePortalShortcut();

    public void Dispose()
    {
        _backend.HotkeyPressed -= OnBackendHotkeyPressed;
        _backend.StatusChanged -= OnBackendStatusChanged;
        _backend.Dispose();
    }

    private void OnBackendHotkeyPressed(object? sender, EventArgs e) => HotkeyPressed?.Invoke(this, e);
    private void OnBackendStatusChanged(object? sender, EventArgs e) => StatusChanged?.Invoke(this, e);
}

internal interface IQuickFillHotkeyBackend : IDisposable
{
    event EventHandler? HotkeyPressed;
    event EventHandler? StatusChanged;
    bool IsRegistered { get; }
    string Status { get; }
    QuickFillPortalHotkeyState PortalState { get; }
    bool CanConfigurePortalShortcut { get; }
    void Start(QuickFillSettings settings);
    void Stop();
    void ConfigurePortalShortcut();
}

internal static class QuickFillHotkeyBackendSelector
{
    public static IQuickFillHotkeyBackend Select() => new CompositeGlobalHotkeyBackend();
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShellKrypt.Application.Settings;
using Tmds.DBus.Protocol;

namespace ShellKrypt.Desktop.Services;

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
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkK = 0x4B;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int KeyPress = 2;
    private const int KeyRelease = 3;
    private const int ControlMask = 1 << 2;
    private const int Mod1Mask = 1 << 3;
    private const ulong XK_K = 0x004b;
    private const string PortalService = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    private const string GlobalShortcutsInterface = "org.freedesktop.portal.GlobalShortcuts";
    private const string RequestInterface = "org.freedesktop.portal.Request";
    private const string ShortcutId = "quick-fill";

    private readonly LowLevelKeyboardProc _proc;
    private nint _hookId;
    private nint _xDisplay;
    private Thread? _xThread;
    private DBusConnection? _portalConnection;
    private IDisposable? _portalActivatedMatch;
    private IDisposable? _portalShortcutsChangedMatch;
    private IDisposable? _portalResponseMatch;
    private readonly SemaphoreSlim _portalRequestLock = new(1, 1);
    private CancellationTokenSource? _portalCts;
    private ObjectPath? _portalSessionHandle;
    private uint _portalVersion;
    private bool _portalConfigured;
    private bool _portalRegistered;
    private volatile bool _xRunning;
    private bool _shortcutDown;

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? StatusChanged;

    public bool IsRegistered => _hookId != 0 || _xRunning || _portalRegistered;
    public string Status { get; private set; } = "";
    public QuickFillPortalHotkeyState PortalState { get; private set; } = QuickFillPortalHotkeyState.PortalUnavailable;
    public bool CanConfigurePortalShortcut =>
        IsWaylandSession() &&
        _portalConnection is not null &&
        _portalSessionHandle is not null &&
        _portalVersion >= 2 &&
        PortalState == QuickFillPortalHotkeyState.PortalNeedsConfiguration;

    public void Start(QuickFillSettings settings)
    {
        Stop();
        settings.Normalize();

        if (!settings.GlobalHotkeyEnabled)
        {
            SetStatus("Global shortcut disabled.");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
            SetStatus(_hookId == 0
                ? "Global shortcut could not be registered. Use the in-app Quick Fill page."
                : $"{settings.GlobalShortcut} ready.");
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            if (IsWaylandSession())
            {
                SetPortalState(QuickFillPortalHotkeyState.PortalSessionCreated, "Checking desktop portal Quick Fill shortcut...");
                StartPortalHotkey(settings);
                return;
            }

            if (StartX11Hotkey(settings))
                return;

            SetStatus("Global shortcut could not be registered on this Linux session.");
            return;
        }

        SetStatus("Global shortcut is not available on this platform.");
    }

    public void Stop()
    {
        if (_hookId != 0)
        {
            _ = UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }

        StopX11Hotkey();
        StopPortalHotkey();
        _shortcutDown = false;
    }

    public void Dispose() => Stop();

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var virtualKey = Marshal.ReadInt32(lParam);
            if (virtualKey == VkK && (message == WmKeyDown || message == WmSysKeyDown))
            {
                var ctrl = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                var alt = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
                if (ctrl && alt && !_shortcutDown)
                {
                    _shortcutDown = true;
                    Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                    return 1;
                }
            }
            else if (virtualKey == VkK && (message == WmKeyUp || message == WmSysKeyUp))
            {
                _shortcutDown = false;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool StartX11Hotkey(QuickFillSettings settings)
    {
        if (IsWaylandSession() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return false;

        try
        {
            _xDisplay = XOpenDisplay(null);
            if (_xDisplay == 0)
                return false;

            var root = XDefaultRootWindow(_xDisplay);
            var keycode = XKeysymToKeycode(_xDisplay, XK_K);
            if (keycode == 0)
            {
                StopX11Hotkey();
                return false;
            }

            XGrabKey(_xDisplay, keycode, ControlMask | Mod1Mask, root, true, 1, 1);
            XSelectInput(_xDisplay, root, 1L);
            XFlush(_xDisplay);
            _xRunning = true;
            _xThread = new Thread(X11MessageLoop) { IsBackground = true, Name = "ShellKrypt Quick Fill X11 Hotkey" };
            _xThread.Start();
            SetStatus($"{settings.GlobalShortcut} ready on X11.");
            return true;
        }
        catch
        {
            StopX11Hotkey();
            return false;
        }
    }

    private void StopX11Hotkey()
    {
        _xRunning = false;
        if (_xDisplay != 0)
        {
            try
            {
                XCloseDisplay(_xDisplay);
            }
            catch
            {
            }

            _xDisplay = 0;
        }

        _xThread = null;
    }

    private void X11MessageLoop()
    {
        var shortcutDown = false;
        while (_xRunning && _xDisplay != 0)
        {
            try
            {
                XNextEvent(_xDisplay, out var ev);
                if (ev.Type == KeyPress && !shortcutDown)
                {
                    shortcutDown = true;
                    Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                }
                else if (ev.Type == KeyRelease)
                {
                    shortcutDown = false;
                }
            }
            catch
            {
                return;
            }
        }
    }

    private void StartPortalHotkey(QuickFillSettings settings)
    {
        _portalCts = new CancellationTokenSource();
        _ = StartPortalHotkeyAsync(settings, _portalCts.Token);
    }

    private async Task StartPortalHotkeyAsync(QuickFillSettings settings, CancellationToken ct)
    {
        try
        {
            var sessionAddress = DBusAddress.Session ?? throw new InvalidOperationException("D-Bus session address is unavailable.");
            _portalConnection = new DBusConnection(sessionAddress);
            await _portalConnection.ConnectAsync();

            _portalVersion = await ReadPortalVersionAsync(ct);
            if (_portalVersion == 0)
            {
                SetPortalState(QuickFillPortalHotkeyState.PortalUnavailable, "Portal shortcut failed: Global Shortcuts portal is unavailable.");
                return;
            }

            var token = $"shellkrypt_{Guid.NewGuid():N}";
            var session = await CreatePortalSessionAsync(token, ct);
            if (session is null)
            {
                SetPortalState(QuickFillPortalHotkeyState.PortalFailed, "Portal shortcut failed: session was not created.");
                return;
            }

            _portalSessionHandle = session.Value;
            SetPortalState(QuickFillPortalHotkeyState.PortalSessionCreated, "Desktop portal shortcut session created.");
            await WatchPortalSignalsAsync(session.Value);
            _portalRegistered = true;

            var listed = await ListPortalShortcutsAsync(session.Value, ct);
            if (ApplyPortalShortcutState(listed))
                return;

            await BindPortalShortcutAsync(session.Value, settings.GlobalShortcut, ct);
            SetPortalState(QuickFillPortalHotkeyState.PortalShortcutBound, "Desktop portal Quick Fill shortcut requested.");

            listed = await ListPortalShortcutsAsync(session.Value, ct);
            _ = ApplyPortalShortcutState(listed);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _portalRegistered = false;
            SetPortalState(QuickFillPortalHotkeyState.PortalFailed, $"Portal shortcut failed: {ex.Message}");
        }
    }

    public void ConfigurePortalShortcut()
    {
        if (_portalConnection is null || _portalSessionHandle is null)
        {
            SetPortalState(QuickFillPortalHotkeyState.PortalUnavailable, "Portal shortcut failed: desktop portal session is unavailable.");
            return;
        }

        if (_portalVersion < 2)
        {
            SetPortalState(QuickFillPortalHotkeyState.PortalUnsupportedVersion, "Portal shortcut configuration is not supported by this desktop portal version.");
            return;
        }

        if (_portalConfigured)
            return;

        _portalConfigured = true;
        _ = ConfigurePortalShortcutAsync(_portalSessionHandle.Value, _portalCts?.Token ?? CancellationToken.None);
    }

    private async Task ConfigurePortalShortcutAsync(ObjectPath session, CancellationToken ct)
    {
        try
        {
            MessageBuffer message;
            using (var writer = _portalConnection!.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(PortalService, PortalPath, GlobalShortcutsInterface, "ConfigureShortcuts", "osa{sv}", MessageFlags.None);
                writer.WriteObjectPath(session);
                writer.WriteString("");
                writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
                message = writer.CreateMessage();
            }

            await _portalConnection!.CallMethodAsync(message);
            var listed = await ListPortalShortcutsAsync(session, ct);
            _ = ApplyPortalShortcutState(listed);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            SetPortalState(QuickFillPortalHotkeyState.PortalFailed, $"Portal shortcut failed: {ex.Message}");
        }
        finally
        {
            _portalConfigured = false;
        }
    }

    private async Task<uint> ReadPortalVersionAsync(CancellationToken ct)
    {
        MessageBuffer message;
        using (var writer = _portalConnection!.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(PortalService, PortalPath, PropertiesInterface, "Get", "ss", MessageFlags.None);
            writer.WriteString(GlobalShortcutsInterface);
            writer.WriteString("version");
            message = writer.CreateMessage();
        }

        return await _portalConnection.CallMethodAsync(
            message,
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                return reader.ReadVariantValue().GetUInt32();
            },
            null).WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    private async Task<ObjectPath?> CreatePortalSessionAsync(string token, CancellationToken ct)
    {
        await _portalRequestLock.WaitAsync(ct);
        try
        {
            var responseTask = WaitForPortalResponseAsync(ct);

            MessageBuffer message;
            using (var writer = _portalConnection!.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(PortalService, PortalPath, GlobalShortcutsInterface, "CreateSession", "a{sv}", MessageFlags.None);
                writer.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    ["session_handle_token"] = token
                });
                message = writer.CreateMessage();
            }

            _ = await _portalConnection.CallMethodAsync<ObjectPath>(
                message,
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return reader.ReadObjectPath();
                },
                null);

            var response = await responseTask;
            if (response.Response != 0)
                throw new InvalidOperationException($"portal rejected shortcut session ({response.Response}).");

            if (!response.Results.TryGetValue("session_handle", out var value))
                return default(ObjectPath?);

            return value.GetObjectPath();
        }
        finally
        {
            _portalRequestLock.Release();
        }
    }

    private async Task BindPortalShortcutAsync(ObjectPath session, string shortcut, CancellationToken ct)
    {
        await _portalRequestLock.WaitAsync(ct);
        try
        {
            var responseTask = WaitForPortalResponseAsync(ct);

            MessageBuffer message;
            using (var writer = _portalConnection!.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(PortalService, PortalPath, GlobalShortcutsInterface, "BindShortcuts", "oa(sa{sv})sa{sv}", MessageFlags.None);
                writer.WriteObjectPath(session);
                var shortcuts = writer.WriteArrayStart(DBusType.Struct);
                writer.WriteStructureStart();
                writer.WriteString(ShortcutId);
                writer.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    ["description"] = "Open ShellKrypt Quick Fill",
                    ["preferred_trigger"] = ShortcutToPortalTrigger(shortcut)
                });
                writer.WriteArrayEnd(shortcuts);
                writer.WriteString("");
                writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
                message = writer.CreateMessage();
            }

            _ = await _portalConnection.CallMethodAsync<ObjectPath>(
                message,
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return reader.ReadObjectPath();
                },
                null);

            var response = await responseTask;
            if (response.Response != 0)
                throw new InvalidOperationException($"portal rejected shortcut binding ({response.Response}).");
        }
        finally
        {
            _portalRequestLock.Release();
        }
    }

    private async Task<IReadOnlyList<PortalShortcut>> ListPortalShortcutsAsync(ObjectPath session, CancellationToken ct)
    {
        await _portalRequestLock.WaitAsync(ct);
        try
        {
            var responseTask = WaitForPortalResponseAsync(ct);

            MessageBuffer message;
            using (var writer = _portalConnection!.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(PortalService, PortalPath, GlobalShortcutsInterface, "ListShortcuts", "oa{sv}", MessageFlags.None);
                writer.WriteObjectPath(session);
                writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
                message = writer.CreateMessage();
            }

            _ = await _portalConnection.CallMethodAsync<ObjectPath>(
                message,
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return reader.ReadObjectPath();
                },
                null);

            var response = await responseTask;
            if (response.Response != 0)
                throw new InvalidOperationException($"portal rejected shortcut listing ({response.Response}).");

            SetPortalState(QuickFillPortalHotkeyState.PortalShortcutListed, "Desktop portal Quick Fill shortcuts listed.");
            return ParsePortalShortcuts(response.Results);
        }
        finally
        {
            _portalRequestLock.Release();
        }
    }

    private async Task<PortalResponse> WaitForPortalResponseAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _portalResponseMatch?.Dispose();
        _portalResponseMatch = await _portalConnection!.AddMatchAsync(
            new MatchRule
            {
                Type = MessageType.Signal,
                Sender = PortalService,
                Interface = RequestInterface,
                Member = "Response",
                PathNamespace = "/org/freedesktop/portal/desktop/request"
            },
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                var response = reader.ReadUInt32();
                var results = reader.ReadDictionaryOfStringToVariantValue();
                var path = message.PathIsSet ? Encoding.UTF8.GetString(message.Path) : "";
                return new PortalResponse(path, response, results);
            },
            static (exception, response, _, state) =>
            {
                var completion = (TaskCompletionSource<PortalResponse>)state!;
                if (exception is not null)
                    completion.TrySetException(exception);
                else
                    completion.TrySetResult(response);
            },
            ObserverFlags.EmitOnDispose,
            null,
            tcs,
            false);

        await using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
    }

    private async Task WatchPortalSignalsAsync(ObjectPath session)
    {
        _portalActivatedMatch?.Dispose();
        _portalActivatedMatch = await _portalConnection!.AddMatchAsync(
            new MatchRule
            {
                Type = MessageType.Signal,
                Sender = PortalService,
                Interface = GlobalShortcutsInterface,
                Member = "Activated",
                Path = PortalPath
            },
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                var sessionHandle = reader.ReadObjectPath();
                var shortcutId = reader.ReadString();
                _ = reader.ReadUInt64();
                var options = reader.ReadDictionaryOfStringToVariantValue();
                var activationToken = options.TryGetValue("activation_token", out var token)
                    ? token.GetString()
                    : "";
                return new PortalActivation(sessionHandle, shortcutId, activationToken);
            },
            static (exception, activation, _, state) =>
            {
                if (exception is not null)
                    return;

                var service = (GlobalHotkeyService)state!;
                if (service._portalSessionHandle is { } sessionHandle &&
                    activation.SessionHandle == sessionHandle &&
                    string.Equals(activation.ShortcutId, ShortcutId, StringComparison.Ordinal))
                {
                    // Avalonia does not expose a stable cross-platform API for forwarding the
                    // portal activation token yet; popup activation remains compositor-dependent.
                    Dispatcher.UIThread.Post(() => service.HotkeyPressed?.Invoke(service, EventArgs.Empty));
                }
            },
            ObserverFlags.None,
            null,
            this,
            false);

        _portalShortcutsChangedMatch?.Dispose();
        _portalShortcutsChangedMatch = await _portalConnection!.AddMatchAsync(
            new MatchRule
            {
                Type = MessageType.Signal,
                Sender = PortalService,
                Interface = GlobalShortcutsInterface,
                Member = "ShortcutsChanged",
                Path = PortalPath
            },
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                return reader.ReadObjectPath();
            },
            static (exception, sessionHandle, _, state) =>
            {
                if (exception is not null)
                    return;

                var service = (GlobalHotkeyService)state!;
                if (service._portalSessionHandle is { } current && sessionHandle == current)
                    _ = service.RefreshPortalShortcutsAsync();
            },
            ObserverFlags.None,
            null,
            this,
            false);
    }

    private async Task RefreshPortalShortcutsAsync()
    {
        if (_portalSessionHandle is not { } session || _portalConnection is null || _portalCts?.IsCancellationRequested == true)
            return;

        try
        {
            var listed = await ListPortalShortcutsAsync(session, _portalCts?.Token ?? CancellationToken.None);
            _ = ApplyPortalShortcutState(listed);
        }
        catch (Exception ex) when (_portalCts?.IsCancellationRequested != true)
        {
            SetPortalState(QuickFillPortalHotkeyState.PortalFailed, $"Portal shortcut failed: {ex.Message}");
        }
    }

    private bool ApplyPortalShortcutState(IReadOnlyList<PortalShortcut> shortcuts)
    {
        var shortcut = FindQuickFillShortcut(shortcuts);
        if (shortcut is not null && !string.IsNullOrWhiteSpace(shortcut.TriggerDescription))
        {
            SetPortalState(QuickFillPortalHotkeyState.PortalReady, $"Quick Fill shortcut ready: {shortcut.TriggerDescription}");
            return true;
        }

        SetPortalState(QuickFillPortalHotkeyState.PortalNeedsConfiguration, "Quick Fill shortcut needs configuration");
        return false;
    }

    private void StopPortalHotkey()
    {
        _portalRegistered = false;
        _portalConfigured = false;
        _portalVersion = 0;
        _portalCts?.Cancel();
        _portalCts?.Dispose();
        _portalCts = null;
        _portalActivatedMatch?.Dispose();
        _portalActivatedMatch = null;
        _portalShortcutsChangedMatch?.Dispose();
        _portalShortcutsChangedMatch = null;
        _portalResponseMatch?.Dispose();
        _portalResponseMatch = null;
        _portalConnection?.Dispose();
        _portalConnection = null;
        _portalSessionHandle = null;
        PortalState = QuickFillPortalHotkeyState.PortalUnavailable;
    }

    private static bool IsWaylandSession()
        => string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
           !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private void SetStatus(string value)
    {
        Status = value;
        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, EventArgs.Empty));
    }

    private void SetPortalState(QuickFillPortalHotkeyState state, string status)
    {
        PortalState = state;
        SetStatus(status);
    }

    private static string ShortcutToPortalTrigger(string shortcut)
        => string.IsNullOrWhiteSpace(shortcut)
            ? "CTRL+ALT+K"
            : shortcut.Replace("Ctrl", "CTRL", StringComparison.OrdinalIgnoreCase)
                .Replace("Alt", "ALT", StringComparison.OrdinalIgnoreCase)
                .Replace("+", "+", StringComparison.Ordinal);

    private static IReadOnlyList<PortalShortcut> ParsePortalShortcuts(Dictionary<string, VariantValue> results)
    {
        if (!results.TryGetValue("shortcuts", out var shortcuts))
            return Array.Empty<PortalShortcut>();

        var parsed = new List<PortalShortcut>();
        for (var i = 0; i < shortcuts.Count; i++)
        {
            var shortcut = shortcuts.GetItem(i);
            var id = shortcut.GetItem(0).GetString();
            var properties = shortcut.GetItem(1).GetDictionary<string, VariantValue>();
            var trigger = properties.TryGetValue("trigger_description", out var triggerDescription)
                ? triggerDescription.GetString()
                : "";
            parsed.Add(new PortalShortcut(id, trigger));
        }

        return parsed;
    }

    private static PortalShortcut? FindQuickFillShortcut(IReadOnlyList<PortalShortcut> shortcuts)
    {
        foreach (var shortcut in shortcuts)
        {
            if (string.Equals(shortcut.Id, ShortcutId, StringComparison.Ordinal))
                return shortcut;
        }

        return null;
    }

    private sealed record PortalResponse(string Path, uint Response, Dictionary<string, VariantValue> Results);

    private sealed record PortalActivation(ObjectPath SessionHandle, string ShortcutId, string ActivationToken);

    private sealed record PortalShortcut(string Id, string TriggerDescription);

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("libX11")]
    private static extern nint XOpenDisplay(string? display);

    [DllImport("libX11")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11")]
    private static extern nint XDefaultRootWindow(nint display);

    [DllImport("libX11")]
    private static extern int XKeysymToKeycode(nint display, ulong keysym);

    [DllImport("libX11")]
    private static extern int XGrabKey(nint display, int keycode, int modifiers, nint grabWindow, bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport("libX11")]
    private static extern int XSelectInput(nint display, nint window, long eventMask);

    [DllImport("libX11")]
    private static extern int XFlush(nint display);

    [DllImport("libX11")]
    private static extern int XNextEvent(nint display, out XEvent ev);

    [StructLayout(LayoutKind.Sequential)]
    private struct XEvent
    {
        public int Type;
        private readonly nint pad1;
        private readonly nint pad2;
        private readonly nint pad3;
        private readonly nint pad4;
        private readonly nint pad5;
        private readonly nint pad6;
        private readonly nint pad7;
        private readonly nint pad8;
        private readonly nint pad9;
        private readonly nint pad10;
        private readonly nint pad11;
        private readonly nint pad12;
        private readonly nint pad13;
        private readonly nint pad14;
        private readonly nint pad15;
        private readonly nint pad16;
        private readonly nint pad17;
        private readonly nint pad18;
        private readonly nint pad19;
        private readonly nint pad20;
        private readonly nint pad21;
        private readonly nint pad22;
        private readonly nint pad23;
        private readonly nint pad24;
    }
}

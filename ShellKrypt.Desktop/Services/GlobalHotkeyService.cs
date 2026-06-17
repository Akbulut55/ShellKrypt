using System;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Services;

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

    private readonly LowLevelKeyboardProc _proc;
    private nint _hookId;
    private bool _shortcutDown;

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
    }

    public event EventHandler? HotkeyPressed;

    public bool IsRegistered => _hookId != 0;
    public string Status { get; private set; } = "";

    public void Start(QuickFillSettings settings)
    {
        Stop();
        settings.Normalize();

        if (!settings.GlobalHotkeyEnabled)
        {
            Status = "Global shortcut disabled.";
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            Status = "Global shortcut is available on Windows only.";
            return;
        }

        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        Status = _hookId == 0
            ? "Global shortcut could not be registered. Use the in-app Quick Fill page."
            : $"{settings.GlobalShortcut} ready.";
    }

    public void Stop()
    {
        if (_hookId != 0)
        {
            _ = UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }

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
}

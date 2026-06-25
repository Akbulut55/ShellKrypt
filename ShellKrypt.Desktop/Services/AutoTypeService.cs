using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services;

public enum AutoTypeStepKind
{
    Text = 1,
    Key = 2,
    Delay = 4
}

public sealed record AutoTypeStep(
    AutoTypeStepKind Kind,
    string Text = "",
    int DelayMilliseconds = 0,
    QuickFillKeystrokeKind Key = QuickFillKeystrokeKind.Tab,
    QuickFillKeyModifiers Modifiers = QuickFillKeyModifiers.None,
    int RepeatCount = 1);

public sealed class AutoTypeService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyAlt = 0x12;
    private const ushort VirtualKeyMeta = 0x5B;

    public async Task<bool> SendAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct = default)
    {
        if (targetWindowHandle == 0 || steps.Count == 0)
            return false;

        if (OperatingSystem.IsLinux())
            return await SendLinuxAsync(targetWindowHandle, steps, ct);

        if (!OperatingSystem.IsWindows())
            return false;

        if (!SetForegroundWindow(targetWindowHandle))
            return false;

        await Task.Delay(120, ct);
        if (GetForegroundWindow() != targetWindowHandle)
            return false;

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            switch (step.Kind)
            {
                case AutoTypeStepKind.Text:
                    SendText(step.Text);
                    break;
                case AutoTypeStepKind.Key:
                    SendKey(step.Key, step.Modifiers, step.RepeatCount);
                    break;
                case AutoTypeStepKind.Delay:
                    await Task.Delay(Math.Clamp(step.DelayMilliseconds, 0, 10_000), ct);
                    break;
            }
        }

        return true;
    }

    private static async Task<bool> SendLinuxAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct)
    {
        if (IsWaylandSession() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return false;

        var display = XOpenDisplay(null);
        if (display == 0)
            return false;

        try
        {
            XSetInputFocus(display, targetWindowHandle, 1, 0);
            XFlush(display);
            await Task.Delay(120, ct);

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                switch (step.Kind)
                {
                    case AutoTypeStepKind.Text:
                        SendLinuxText(display, step.Text);
                        break;
                    case AutoTypeStepKind.Key:
                        SendLinuxKey(display, step.Key, step.Modifiers, step.RepeatCount);
                        break;
                    case AutoTypeStepKind.Delay:
                        await Task.Delay(Math.Clamp(step.DelayMilliseconds, 0, 10_000), ct);
                        break;
                }
            }

            XFlush(display);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    private static void SendLinuxText(nint display, string text)
    {
        foreach (var ch in text)
        {
            if (!TryMapLinuxChar(ch, out var keysym, out var needsShift))
                continue;

            var keycode = XKeysymToKeycode(display, keysym);
            if (keycode == 0)
                continue;

            if (needsShift)
                SendLinuxKeyCode(display, XKeysymToKeycode(display, 0xffe1), true);
            SendLinuxKeyCode(display, keycode, true);
            SendLinuxKeyCode(display, keycode, false);
            if (needsShift)
                SendLinuxKeyCode(display, XKeysymToKeycode(display, 0xffe1), false);
        }
    }

    private static void SendLinuxKey(nint display, QuickFillKeystrokeKind key, QuickFillKeyModifiers modifiers, int repeatCount)
    {
        if (!TryMapLinuxKeysym(key, out var keysym))
            return;

        var modifierKeycodes = LinuxModifierKeycodes(display, modifiers).Where(code => code != 0).ToArray();
        foreach (var code in modifierKeycodes)
            SendLinuxKeyCode(display, code, true);

        var keycode = XKeysymToKeycode(display, keysym);
        var repeat = Math.Clamp(repeatCount <= 0 ? 1 : repeatCount, 1, 100);
        for (var i = 0; i < repeat; i++)
        {
            SendLinuxKeyCode(display, keycode, true);
            SendLinuxKeyCode(display, keycode, false);
        }

        for (var i = modifierKeycodes.Length - 1; i >= 0; i--)
            SendLinuxKeyCode(display, modifierKeycodes[i], false);
    }

    private static IEnumerable<uint> LinuxModifierKeycodes(nint display, QuickFillKeyModifiers modifiers)
    {
        if (modifiers.HasFlag(QuickFillKeyModifiers.Ctrl))
            yield return XKeysymToKeycode(display, 0xffe3);
        if (modifiers.HasFlag(QuickFillKeyModifiers.Alt))
            yield return XKeysymToKeycode(display, 0xffe9);
        if (modifiers.HasFlag(QuickFillKeyModifiers.Shift))
            yield return XKeysymToKeycode(display, 0xffe1);
        if (modifiers.HasFlag(QuickFillKeyModifiers.Meta))
            yield return XKeysymToKeycode(display, 0xffeb);
    }

    private static void SendLinuxKeyCode(nint display, uint keycode, bool down)
    {
        _ = XTestFakeKeyEvent(display, keycode, down, 0);
        XFlush(display);
    }

    private static bool TryMapLinuxChar(char ch, out ulong keysym, out bool needsShift)
    {
        needsShift = false;
        if (ch is >= 'a' and <= 'z')
        {
            keysym = ch;
            return true;
        }
        if (ch is >= 'A' and <= 'Z')
        {
            keysym = char.ToLowerInvariant(ch);
            needsShift = true;
            return true;
        }
        if (ch is >= '0' and <= '9')
        {
            keysym = ch;
            return true;
        }

        keysym = ch switch
        {
            ' ' => 0x020,
            '\t' => 0xff09,
            '\n' => 0xff0d,
            '.' => 0x02e,
            ',' => 0x02c,
            '-' => 0x02d,
            '_' => 0x02d,
            '@' => 0x032,
            ':' => 0x03b,
            '/' => 0x02f,
            '\\' => 0x05c,
            _ => 0
        };
        needsShift = ch is '_' or '@' or ':';
        return keysym != 0;
    }

    private static bool TryMapLinuxKeysym(QuickFillKeystrokeKind key, out ulong keysym)
    {
        keysym = key switch
        {
            QuickFillKeystrokeKind.Tab => 0xff09,
            QuickFillKeystrokeKind.Enter => 0xff0d,
            QuickFillKeystrokeKind.Escape => 0xff1b,
            QuickFillKeystrokeKind.Space => 0x020,
            QuickFillKeystrokeKind.Backspace => 0xff08,
            QuickFillKeystrokeKind.Delete => 0xffff,
            QuickFillKeystrokeKind.ArrowLeft => 0xff51,
            QuickFillKeystrokeKind.ArrowUp => 0xff52,
            QuickFillKeystrokeKind.ArrowRight => 0xff53,
            QuickFillKeystrokeKind.ArrowDown => 0xff54,
            QuickFillKeystrokeKind.Home => 0xff50,
            QuickFillKeystrokeKind.End => 0xff57,
            QuickFillKeystrokeKind.PageUp => 0xff55,
            QuickFillKeystrokeKind.PageDown => 0xff56,
            QuickFillKeystrokeKind.Insert => 0xff63,
            >= QuickFillKeystrokeKind.F1 and <= QuickFillKeystrokeKind.F12 => (ulong)(0xffbe + (key - QuickFillKeystrokeKind.F1)),
            >= QuickFillKeystrokeKind.A and <= QuickFillKeystrokeKind.Z => (ulong)('a' + (key - QuickFillKeystrokeKind.A)),
            >= QuickFillKeystrokeKind.D0 and <= QuickFillKeystrokeKind.D9 => (ulong)('0' + (key - QuickFillKeystrokeKind.D0)),
            _ => 0
        };
        return keysym != 0;
    }

    private static bool IsWaylandSession()
        => string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
           !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static void SendText(string text)
    {
        foreach (var ch in text)
        {
            var inputs = new[]
            {
                CreateUnicodeInput(ch, keyUp: false),
                CreateUnicodeInput(ch, keyUp: true)
            };
            _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        }
    }

    private static void SendKey(QuickFillKeystrokeKind key, QuickFillKeyModifiers modifiers, int repeatCount)
    {
        if (!TryMapVirtualKey(key, out var virtualKey))
            return;

        var modifierKeys = ModifierVirtualKeys(modifiers).ToArray();
        foreach (var modifier in modifierKeys)
            SendVirtualKey(modifier, keyUp: false);

        var repeat = Math.Clamp(repeatCount <= 0 ? 1 : repeatCount, 1, 100);
        for (var i = 0; i < repeat; i++)
        {
            SendVirtualKey(virtualKey, keyUp: false);
            SendVirtualKey(virtualKey, keyUp: true);
        }

        for (var i = modifierKeys.Length - 1; i >= 0; i--)
            SendVirtualKey(modifierKeys[i], keyUp: true);
    }

    private static void SendVirtualKey(ushort key, bool keyUp)
    {
        var input = CreateVirtualKeyInput(key, keyUp);
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static IEnumerable<ushort> ModifierVirtualKeys(QuickFillKeyModifiers modifiers)
    {
        if (modifiers.HasFlag(QuickFillKeyModifiers.Ctrl))
            yield return VirtualKeyControl;
        if (modifiers.HasFlag(QuickFillKeyModifiers.Alt))
            yield return VirtualKeyAlt;
        if (modifiers.HasFlag(QuickFillKeyModifiers.Shift))
            yield return VirtualKeyShift;
        if (modifiers.HasFlag(QuickFillKeyModifiers.Meta))
            yield return VirtualKeyMeta;
    }

    private static bool TryMapVirtualKey(QuickFillKeystrokeKind key, out ushort virtualKey)
    {
        virtualKey = key switch
        {
            QuickFillKeystrokeKind.Tab => 0x09,
            QuickFillKeystrokeKind.Enter => 0x0D,
            QuickFillKeystrokeKind.Escape => 0x1B,
            QuickFillKeystrokeKind.Space => 0x20,
            QuickFillKeystrokeKind.Backspace => 0x08,
            QuickFillKeystrokeKind.Delete => 0x2E,
            QuickFillKeystrokeKind.ArrowLeft => 0x25,
            QuickFillKeystrokeKind.ArrowUp => 0x26,
            QuickFillKeystrokeKind.ArrowRight => 0x27,
            QuickFillKeystrokeKind.ArrowDown => 0x28,
            QuickFillKeystrokeKind.Home => 0x24,
            QuickFillKeystrokeKind.End => 0x23,
            QuickFillKeystrokeKind.PageUp => 0x21,
            QuickFillKeystrokeKind.PageDown => 0x22,
            QuickFillKeystrokeKind.Insert => 0x2D,
            >= QuickFillKeystrokeKind.F1 and <= QuickFillKeystrokeKind.F12 => (ushort)(0x70 + (key - QuickFillKeystrokeKind.F1)),
            >= QuickFillKeystrokeKind.A and <= QuickFillKeystrokeKind.Z => (ushort)('A' + (key - QuickFillKeystrokeKind.A)),
            >= QuickFillKeystrokeKind.D0 and <= QuickFillKeystrokeKind.D9 => (ushort)('0' + (key - QuickFillKeystrokeKind.D0)),
            _ => 0
        };

        return virtualKey != 0;
    }

    private static void SendVirtualKey(ushort key)
    {
        var inputs = new[]
        {
            CreateVirtualKeyInput(key, keyUp: false),
            CreateVirtualKeyInput(key, keyUp: true)
        };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input CreateUnicodeInput(char ch, bool keyUp)
        => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Scan = ch,
                    Flags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };

    private static Input CreateVirtualKeyInput(ushort key, bool keyUp)
        => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = key,
                    Flags = keyUp ? KeyEventFKeyUp : 0
                }
            }
        };

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("libX11")]
    private static extern nint XOpenDisplay(string? display);

    [DllImport("libX11")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11")]
    private static extern int XSetInputFocus(nint display, nint window, int revertTo, long time);

    [DllImport("libX11")]
    private static extern uint XKeysymToKeycode(nint display, ulong keysym);

    [DllImport("libX11")]
    private static extern int XFlush(nint display);

    [DllImport("libXtst")]
    private static extern int XTestFakeKeyEvent(nint display, uint keycode, bool isPress, ulong delay);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}

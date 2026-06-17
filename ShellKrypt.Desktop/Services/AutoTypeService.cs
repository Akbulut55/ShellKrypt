using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Services;

public enum AutoTypeStepKind
{
    Text = 1,
    Tab = 2,
    Enter = 3,
    Delay = 4
}

public sealed record AutoTypeStep(AutoTypeStepKind Kind, string Text = "", int DelayMilliseconds = 0);

public sealed class AutoTypeService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VirtualKeyTab = 0x09;
    private const ushort VirtualKeyEnter = 0x0D;

    public async Task<bool> SendAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows() || targetWindowHandle == 0 || steps.Count == 0)
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
                case AutoTypeStepKind.Tab:
                    SendVirtualKey(VirtualKeyTab);
                    break;
                case AutoTypeStepKind.Enter:
                    SendVirtualKey(VirtualKeyEnter);
                    break;
                case AutoTypeStepKind.Delay:
                    await Task.Delay(Math.Clamp(step.DelayMilliseconds, 0, 10_000), ct);
                    break;
            }
        }

        return true;
    }

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

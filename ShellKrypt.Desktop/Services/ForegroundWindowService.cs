using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services;

public sealed class ForegroundWindowService
{
    public QuickFillTargetContext Capture()
    {
        if (!OperatingSystem.IsWindows())
            return new QuickFillTargetContext("", "");

        var handle = GetForegroundWindow();
        if (handle == 0)
            return new QuickFillTargetContext("", "");

        var processName = "";
        _ = GetWindowThreadProcessId(handle, out var processId);
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
        }

        return new QuickFillTargetContext(processName, GetWindowTitle(handle), handle);
    }

    private static string GetWindowTitle(nint handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
            return "";

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}

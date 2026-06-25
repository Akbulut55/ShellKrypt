using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services;

public sealed class ForegroundWindowService
{
    public string Status { get; private set; } = "";

    public QuickFillTargetContext Capture()
    {
        Status = "";
        if (!OperatingSystem.IsWindows())
            return OperatingSystem.IsLinux() ? CaptureLinux() : new QuickFillTargetContext("", "");

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

    private QuickFillTargetContext CaptureLinux()
    {
        if (IsWaylandSession())
        {
            Status = "Target capture is limited by this Wayland compositor. Use X11 for automatic target capture, or create the entry from the manager.";
            return new QuickFillTargetContext("", "");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return new QuickFillTargetContext("", "");

        try
        {
            var active = Run("xprop", "-root _NET_ACTIVE_WINDOW");
            var match = Regex.Match(active, @"0x[0-9a-fA-F]+");
            if (!match.Success || string.Equals(match.Value, "0x0", StringComparison.OrdinalIgnoreCase))
                return new QuickFillTargetContext("", "");

            var windowId = match.Value;
            var title = ParseQuotedValue(Run("xprop", "-id", windowId, "_NET_WM_NAME", "WM_NAME"));
            var pidText = Run("xprop", "-id", windowId, "_NET_WM_PID");
            var pidMatch = Regex.Match(pidText, @"\d+");
            var processName = "";
            if (pidMatch.Success && int.TryParse(pidMatch.Value, out var pid))
            {
                try
                {
                    processName = Process.GetProcessById(pid).ProcessName;
                }
                catch
                {
                    processName = Path.GetFileNameWithoutExtension(File.Exists($"/proc/{pid}/comm") ? File.ReadAllText($"/proc/{pid}/comm").Trim() : "");
                }
            }

            return new QuickFillTargetContext(processName, title, unchecked((nint)Convert.ToInt64(windowId[2..], 16)));
        }
        catch (Exception ex)
        {
            Status = $"Linux target capture failed: {ex.Message}";
            return new QuickFillTargetContext("", "");
        }
    }

    private static string Run(string fileName, params string[] args)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }.WithArguments(args));
        return process?.StandardOutput.ReadToEnd() ?? "";
    }

    private static string ParseQuotedValue(string value)
    {
        var matches = Regex.Matches(value, "\"([^\"]*)\"");
        return matches.Count == 0 ? "" : matches[^1].Groups[1].Value;
    }

    private static bool IsWaylandSession()
        => string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
           !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

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

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo info, params string[] args)
    {
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return info;
    }
}

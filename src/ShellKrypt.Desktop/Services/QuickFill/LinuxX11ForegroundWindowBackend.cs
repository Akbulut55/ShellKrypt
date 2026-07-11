using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services.QuickFill;

internal sealed class LinuxX11ForegroundWindowBackend : IQuickFillTargetCaptureBackend
{
    public string Status { get; private set; } = "";

    public QuickFillTargetContext Capture()
    {
        Status = "";
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

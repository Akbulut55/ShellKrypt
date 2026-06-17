using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.QuickFill;

public static class QuickFillMatcher
{
    public static bool IsMatch(QuickFillEntry entry, QuickFillTargetContext target)
    {
        if (!entry.Enabled)
            return false;

        return IsMatch(entry.Target, target);
    }

    public static bool IsProcessMatch(QuickFillEntry entry, QuickFillTargetContext target)
    {
        if (!entry.Enabled)
            return false;

        return IsProcessMatch(entry.Target, target);
    }

    public static bool IsMatch(QuickFillTargetRule rule, QuickFillTargetContext target)
    {
        if (!IsProcessMatch(rule, target))
        {
            return false;
        }

        var requiredTitle = rule.WindowTitleContains?.Trim();
        return string.IsNullOrWhiteSpace(requiredTitle) ||
               (target.WindowTitle ?? string.Empty).Contains(requiredTitle, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProcessMatch(QuickFillTargetRule rule, QuickFillTargetContext target)
    {
        var process = Normalize(target.ProcessName);
        var requiredProcess = Normalize(rule.ProcessName);
        return !string.IsNullOrWhiteSpace(requiredProcess) &&
               string.Equals(process, requiredProcess, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }
}

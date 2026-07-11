using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddSettingsFindings(HealthAuditOptions options, List<HealthAuditIssue> issues)
    {
        if (!options.AutoLockEnabled)
        {
            AddIssue(
                issues,
                "settings:autolock",
                null,
                HealthAuditSeverity.Medium,
                HealthAuditCategory.AutoLockDisabled,
                "Security Settings",
                "Auto-lock is disabled",
                "Enable auto-lock so unlocked vault sessions close after inactivity.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (!options.LockOnDeactivate)
        {
            AddIssue(
                issues,
                "settings:focus-lock",
                null,
                HealthAuditSeverity.Low,
                HealthAuditCategory.FocusLockDisabled,
                "Security Settings",
                "Focus-loss lock is disabled",
                "Enable lock on app deactivate if you want the vault to lock when ShellKrypt loses focus.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (options.ClipboardClearSeconds > LongClipboardThresholdSeconds)
        {
            AddIssue(
                issues,
                "settings:clipboard-timeout",
                null,
                HealthAuditSeverity.Low,
                HealthAuditCategory.ClipboardTimeoutLong,
                "Clipboard Settings",
                "Clipboard clear timeout is long",
                $"Copied secrets are kept for {options.ClipboardClearSeconds} seconds before best-effort clearing. Consider a shorter timeout.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (options.ClipboardCopyEnabled)
        {
            AddIssue(
                issues,
                "settings:clipboard-copy",
                null,
                HealthAuditSeverity.Info,
                HealthAuditCategory.ClipboardCopyEnabled,
                "Clipboard Settings",
                "Clipboard copy is enabled",
                "Copying secrets can expose them to the operating system clipboard. Clipboard clearing is best-effort, not a security boundary.",
                HealthAuditRecommendedAction.OpenSettings);
        }
    }
}

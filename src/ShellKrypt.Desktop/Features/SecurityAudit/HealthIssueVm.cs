using ShellKrypt.Core.Items;
using System;

namespace ShellKrypt.Desktop.Features.SecurityAudit;

public sealed class HealthIssueVm
{
    public HealthIssueVm(HealthAuditIssue issue, int displayOrder)
    {
        Fingerprint = issue.Fingerprint;
        ItemId = issue.ItemId;
        ItemType = issue.ItemType;
        Severity = issue.Severity;
        Category = issue.Category;
        AffectedItem = issue.AffectedItem;
        Title = issue.Title;
        Details = issue.Details;
        RecommendedAction = issue.RecommendedAction;
        DisplayOrder = displayOrder;
    }

    public string Fingerprint { get; }
    public string ItemId { get; }
    public ItemType? ItemType { get; }
    public HealthAuditSeverity Severity { get; }
    public HealthAuditCategory Category { get; }
    public string AffectedItem { get; }
    public string Title { get; }
    public string Details { get; }
    public HealthAuditRecommendedAction RecommendedAction { get; }
    public int DisplayOrder { get; }
    public int SeverityRank => (int)Severity;
    public string SeverityText => Severity.ToString().ToUpperInvariant();
    public string CategoryText => Category switch
    {
        HealthAuditCategory.EmptyPassword => "Empty Password",
        HealthAuditCategory.WeakPassword => "Weak Password",
        HealthAuditCategory.ReusedPassword => "Reused Password",
        HealthAuditCategory.StaleCredential => "Stale Login",
        HealthAuditCategory.ExpiredCard => "Expired Card",
        HealthAuditCategory.ExpiringCard => "Expiring Card",
        HealthAuditCategory.ReusedApiSecret => "Reused API Secret",
        HealthAuditCategory.OldApiKey => "Old API Key",
        HealthAuditCategory.ApiKeyMissingSecret => "No Secret",
        HealthAuditCategory.AutoLockDisabled => "Auto-lock",
        HealthAuditCategory.FocusLockDisabled => "Focus Lock",
        HealthAuditCategory.ClipboardTimeoutLong => "Clipboard",
        HealthAuditCategory.ClipboardCopyEnabled => "Clipboard",
        _ => Category.ToString()
    };

    public string ScopeKey => ItemType switch
    {
        ShellKrypt.Core.Items.ItemType.Web => HealthViewModel.FilterPasswords,
        ShellKrypt.Core.Items.ItemType.Card => HealthViewModel.FilterCards,
        ShellKrypt.Core.Items.ItemType.ApiKey => HealthViewModel.FilterApiKeys,
        ShellKrypt.Core.Items.ItemType.ProjectSecret => HealthViewModel.FilterProjectSecrets,
        null => HealthViewModel.FilterSettings,
        _ => HealthViewModel.FilterAll
    };

    public string ActionText => RecommendedAction switch
    {
        HealthAuditRecommendedAction.GenerateReplacementPassword => "Generate Fix",
        HealthAuditRecommendedAction.OpenWebLogin => "Open Login",
        HealthAuditRecommendedAction.OpenCard => "Open Card",
        HealthAuditRecommendedAction.OpenApiKey => "Open API Key",
        HealthAuditRecommendedAction.OpenSettings => "Open Settings",
        _ => "Review"
    };

    public bool CanRunPrimaryAction => RecommendedAction != HealthAuditRecommendedAction.None;

    public string SeverityBadgeBackground => Severity switch
    {
        HealthAuditSeverity.Critical => "DangerMutedBrush",
        HealthAuditSeverity.High => "DangerMutedBrush",
        HealthAuditSeverity.Medium => "WarningMutedBrush",
        HealthAuditSeverity.Low => "InfoMutedBrush",
        _ => "SurfaceElevatedBrush"
    };

    public string SeverityBadgeForeground => Severity switch
    {
        HealthAuditSeverity.Critical => "DangerBrush",
        HealthAuditSeverity.High => "DangerBrush",
        HealthAuditSeverity.Medium => "WarningForegroundBrush",
        HealthAuditSeverity.Low => "InfoBrush",
        _ => "TextMutedBrush"
    };

    public string CategoryBadgeBackground => ItemType switch
    {
        ShellKrypt.Core.Items.ItemType.Web => "TypeLoginBackgroundBrush",
        ShellKrypt.Core.Items.ItemType.Card => "TypeCardBackgroundBrush",
        ShellKrypt.Core.Items.ItemType.ApiKey => "TypeApiKeyBackgroundBrush",
        null => "SurfaceElevatedBrush",
        _ => "SurfaceElevatedBrush"
    };

    public string CategoryBadgeForeground => ItemType switch
    {
        ShellKrypt.Core.Items.ItemType.Web => "TypeLoginForegroundBrush",
        ShellKrypt.Core.Items.ItemType.Card => "TypeCardForegroundBrush",
        ShellKrypt.Core.Items.ItemType.ApiKey => "TypeApiKeyForegroundBrush",
        null => "TextSecondaryBrush",
        _ => "TextSecondaryBrush"
    };

    public string SeverityAccentBrush => Severity switch
    {
        HealthAuditSeverity.Critical => "DangerBrush",
        HealthAuditSeverity.High => "DangerBrush",
        HealthAuditSeverity.Medium => "WarningBrush",
        HealthAuditSeverity.Low => "InfoBrush",
        _ => "BorderBrushStrong"
    };

    public string IconGlyph => ItemType switch
    {
        ShellKrypt.Core.Items.ItemType.Web => "PW",
        ShellKrypt.Core.Items.ItemType.Card => "CC",
        ShellKrypt.Core.Items.ItemType.ApiKey => "AK",
        null => "ST",
        _ => "AU"
    };
}

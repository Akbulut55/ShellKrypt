using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Ports;
using ShellKrypt.Desktop.Shell.Dialogs;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public static class ActivityViewDesignData
{
    private static readonly DateTimeOffset PreviewNow = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    public static ActivityViewModel CreateEmpty() => Create(ActivityLogLoadResult.Empty);
    public static ActivityViewModel CreatePopulated() => Create(new(Entries(), 0, ActivityLogFailureKind.None));
    public static ActivityViewModel CreateWarning() => Create(new(Entries(), 2, ActivityLogFailureKind.None));
    public static ActivityViewModel CreateFailure() => Create(new([], 0, ActivityLogFailureKind.ReadFailed));

    private static ActivityViewModel Create(ActivityLogLoadResult result)
    {
        var localization = new LocalizationService();
        var runtime = new ActivityLogsRuntime(new DesignSession(), localization, new DesignRecorder(), new DesignDialogs());
        var model = new ActivityViewModel(runtime, new ActivityLogService(new DesignStore(result)), new FixedTimeProvider(PreviewNow));
        model.Activate();
        return model;
    }

    private static IReadOnlyList<ActivityLogEntry> Entries() =>
    [
        new("a1b2c3d4e5f6", PreviewNow.AddMinutes(-8).ToString("O"), "vault", "Vault unlocked", "Unlocked the synthetic preview vault.", "success", "/preview/Synthetic.skvault") { AffectedItem = "Synthetic" },
        new("b2c3d4e5f6a1", PreviewNow.AddHours(-2).ToString("O"), "audit", "Security audit refreshed", "Reviewed value-free synthetic findings.", "info", "/preview/Synthetic.skvault") { AffectedItem = "Security Audit" },
        new("c3d4e5f6a1b2", PreviewNow.AddDays(-3).ToString("O"), "transfer", "Plaintext report exported", "Saved a synthetic report.", "warning", "/preview/Synthetic.skvault") { AffectedItem = "preview.json" }
    ];

    private sealed class DesignStore(ActivityLogLoadResult result) : IActivityLogStore
    {
        public ActivityLogLoadResult Load(string? vaultPath, byte[]? vaultKey) => result;
        public ActivityLogOperationResult Append(ActivityLogEntry entry, byte[]? vaultKey) => ActivityLogOperationResult.Succeeded;
        public ActivityLogOperationResult Clear(string? vaultPath, byte[]? vaultKey) => ActivityLogOperationResult.Succeeded;
    }

    private sealed class DesignSession : IVaultSessionController
    {
        public string? VaultPath => "/preview/Synthetic.skvault";
        public bool IsUnlocked => true;
        public byte[] VaultKey { get; } = new byte[32];
        public event EventHandler? StateChanged { add { } remove { } }
        public void SetVaultPath(string? path) { }
        public void SetVaultKey(byte[] vaultKey) { }
        public void ClearSensitive() { }
    }

    private sealed class DesignRecorder : IActivityRecorder
    {
        public event EventHandler<ActivityRecorderChangedEventArgs>? Changed { add { } remove { } }
        public ActivityLogOperationResult Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null)
            => ActivityLogOperationResult.Succeeded;
    }

    private sealed class DesignDialogs : IDesktopDialogService
    {
        public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName) => Task.FromResult<string?>(null);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText) => Task.FromResult(false);
        public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) => Task.FromResult(false);
        public Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText) => Task.FromResult<string?>(null);
        public Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null) => Task.FromResult((false, "", ""));
        public Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath) => Task.FromResult((false, displayName, description));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

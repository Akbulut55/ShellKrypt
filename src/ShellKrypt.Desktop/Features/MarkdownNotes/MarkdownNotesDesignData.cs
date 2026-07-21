using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Notes;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public static class MarkdownNotesDesignData
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    public static MarkdownNotesViewModel CreateEmpty() => Create(NoteLoadResult.Empty);
    public static MarkdownNotesViewModel CreatePopulated() => Create(new(Entries(), 0, NoteFailureKind.None), select: false);
    public static MarkdownNotesViewModel CreateSelectedPreview() => Create(new(Entries(), 0, NoteFailureKind.None));
    public static MarkdownNotesViewModel CreateEditing() => Create(new(Entries(), 0, NoteFailureKind.None), edit: true);
    public static MarkdownNotesViewModel CreateSplit() => Create(new(Entries(), 0, NoteFailureKind.None), split: true);
    public static MarkdownNotesViewModel CreateDirty() => Create(new(Entries(), 0, NoteFailureKind.None), dirty: true);
    public static MarkdownNotesViewModel CreateLibraryOpen()
    {
        var model = Create(new(Entries(), 0, NoteFailureKind.None));
        model.IsNoteLibraryOpen = true;
        return model;
    }
    public static MarkdownNotesViewModel CreateAutoSaveDisabledDirty()
        => Create(new(Entries(), 0, NoteFailureKind.None), dirty: true, autoSaveSeconds: 0);
    public static MarkdownNotesViewModel CreateAutoSavePending()
    {
        var model = Create(new(Entries(), 0, NoteFailureKind.None), dirty: true);
        model.AutoSaveStatus = Localized("Notes.AutoSave.Pending");
        return model;
    }
    public static MarkdownNotesViewModel CreateAutoSaveFailure()
    {
        var model = Create(new(Entries(), 0, NoteFailureKind.None), dirty: true);
        model.AutoSaveStatus = model.List.Count > 0 ? Localized("Notes.AutoSave.Failed") : "";
        return model;
    }
    public static MarkdownNotesViewModel CreateWarning() => Create(new(Entries(), 2, NoteFailureKind.None));
    public static MarkdownNotesViewModel CreateLoadFailure() => Create(new([], 0, NoteFailureKind.ReadFailed));

    private static MarkdownNotesViewModel Create(NoteLoadResult result, bool select = true, bool edit = false, bool split = false, bool dirty = false, int autoSaveSeconds = 3)
    {
        var localization = new LocalizationService();
        var liveAutoSaveSeconds = 0;
        var runtime = new MarkdownNotesRuntime(new DesignSession(), localization, new DesignRecorder(), new DesignClipboard(), () => liveAutoSaveSeconds, new DesignDialogs());
        var model = new MarkdownNotesViewModel(runtime, new NoteService(new DesignStore(result)), _ => Task.CompletedTask, new FixedTimeProvider(Now));
        model.ActivateAsync().GetAwaiter().GetResult();
        if (!select)
        {
            model.Document.Clear();
            model.SelectedNote = null;
        }
        else if (edit || split || dirty)
        {
            model.Document.IsEditing = true;
            model.Document.ViewMode = split ? "split" : "editor";
            if (dirty)
                model.Document.Content += "\n\nUnsaved preview-only change.";
        }
        liveAutoSaveSeconds = autoSaveSeconds;
        return model;
    }

    private static IReadOnlyList<NoteEntry> Entries() =>
    [
        new("note-a", "Deployment runbook", "# Deployment\n\n- Verify backup\n- Rotate credentials", true, Now.AddDays(-10).ToString("O"), Now.AddMinutes(-8).ToString("O")),
        new("note-b", "Recovery checklist", "> Synthetic preview content only.", false, Now.AddDays(-8).ToString("O"), Now.AddHours(-2).ToString("O"))
    ];

    private static string Localized(string key) => new LocalizationService().Get(key);

    private sealed class DesignStore(NoteLoadResult result) : INoteStore
    {
        public Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default) => Task.FromResult(result);
        public Task<NoteOperationResult> InsertAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default) => Task.FromResult(NoteOperationResult.Succeeded);
        public Task<NoteOperationResult> UpdateAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default) => Task.FromResult(NoteOperationResult.Succeeded);
        public Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default) => Task.FromResult(NoteOperationResult.Succeeded);
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

    private sealed class DesignClipboard : ISecureClipboardService
    {
        public void Attach(IClipboard? clipboard) { }
        public Task CopyAsync(string text) => Task.CompletedTask;
        public Task ClearAsync() => Task.CompletedTask;
        public Task<Bitmap?> TryGetBitmapAsync() => Task.FromResult<Bitmap?>(null);
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

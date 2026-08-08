using ShellKrypt.Application.Markdown;
using ShellKrypt.Desktop.Features.MarkdownNotes;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class MarkdownNotesStateTests
{
    [Fact]
    public void Runtime_ReadsCurrentAutoSaveSettingInsteadOfCapturedDelay()
    {
        var seconds = 3;
        var runtime = new ShellKrypt.Desktop.Shell.Runtime.MarkdownNotesRuntime(
            null!, null!, null!, null!, () => seconds, null!);

        Assert.Equal(3, runtime.MarkdownAutoSaveSeconds);
        seconds = 0;
        Assert.Equal(0, runtime.MarkdownAutoSaveSeconds);
    }

    [Fact]
    public void Library_OpensAndClosesExplicitly()
    {
        var model = MarkdownNotesDesignData.CreateSelectedPreview();

        model.OpenNoteLibraryCommand.Execute(null);
        Assert.True(model.IsNoteLibraryOpen);

        model.CloseNoteLibraryCommand.Execute(null);
        Assert.False(model.IsNoteLibraryOpen);
    }

    [Fact]
    public void DisabledAutoSave_UsesUnsavedManualStatus()
    {
        var model = MarkdownNotesDesignData.CreateAutoSaveDisabledDirty();

        Assert.False(model.IsAutoSaveEnabled);
        Assert.True(model.Document.IsDirty);
        Assert.Equal("Unsaved changes", model.DocumentStatus);
        Assert.Equal("", model.AutoSaveStatus);
    }

    [Fact]
    public void EditorTyping_DoesNotPublishACompleteTextReplacement()
    {
        var model = MarkdownNotesDesignData.CreateAutoSaveDisabledDirty();
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        model.Document.EditorDocument.Insert(model.Document.EditorDocument.TextLength, "x");

        Assert.DoesNotContain(nameof(MarkdownNotesViewModel.EditorDocument), notifications);
        Assert.True(notifications.Count < 10, $"A single edit raised {notifications.Count} UI notifications.");

        notifications.Clear();
        model.Document.EditorDocument.Insert(model.Document.EditorDocument.TextLength, "y");
        Assert.Empty(notifications);
    }

    [Fact]
    public void DirtyState_StopsPublishingAfterTheDraftIsAlreadyDirty()
    {
        var original = new string('a', 40_000);
        var document = new NoteDocumentState();
        document.Open(Note("large", "Large", original));
        var dirtyChanges = 0;
        document.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NoteDocumentState.IsDirty))
                dirtyChanges++;
        };

        document.Content = original + "x";
        document.Content = original + "xy";
        document.Content = original + "xyz";

        Assert.True(document.IsDirty);
        Assert.Equal(1, dirtyChanges);

        document.Content = original;
        Assert.False(document.IsDirty);
        Assert.Equal(2, dirtyChanges);
    }

    [Fact]
    public void LibrarySearch_DoesNotReplaceDirtyDocument()
    {
        var model = MarkdownNotesDesignData.CreateAutoSaveDisabledDirty();
        var sourceId = model.Document.SourceId;
        var content = model.Document.Content;

        model.NoteLibrarySearchText = "Recovery";

        Assert.Equal(sourceId, model.Document.SourceId);
        Assert.Equal(content, model.Document.Content);
        Assert.Equal("Recovery checklist", Assert.Single(model.NoteLibraryItems).Title);
    }

    [Fact]
    public void Library_IsAlphabeticalAndSearchDoesNotChangeDocument()
    {
        var list = new NoteListState();
        list.Replace([
            Note("b", "Zulu", "needle"),
            Note("a", "alpha", "other")
        ]);
        var document = new NoteDocumentState();
        document.Open(list.Notes[0]);

        list.SearchText = "needle";

        Assert.Equal("Zulu", Assert.Single(list.LibraryNotes).Title);
        Assert.Equal("b", document.SourceId);
        Assert.Equal("needle", document.Content);
    }

    [Fact]
    public void Document_TracksDirtyStateAndCanDiscardOriginal()
    {
        var document = new NoteDocumentState();
        document.Open(Note("a", "Original", "body"));

        document.Title = " Changed ";
        document.Content = "draft";

        Assert.True(document.IsDirty);
        document.DiscardChanges();
        Assert.False(document.IsDirty);
        Assert.Equal("Original", document.Title);
        Assert.Equal("body", document.Content);
        Assert.False(document.IsEditing);
        Assert.False(document.EditorDocument.UndoStack.CanUndo);
    }

    [Fact]
    public void Document_ClearRemovesTextAndUndoHistory()
    {
        var document = new NoteDocumentState();
        document.Open(Note("a", "Sensitive", "original"));
        document.EditorDocument.Insert(document.EditorDocument.TextLength, " secret");
        Assert.True(document.EditorDocument.UndoStack.CanUndo);

        document.Clear();

        Assert.Equal(0, document.EditorDocument.TextLength);
        Assert.False(document.EditorDocument.UndoStack.CanUndo);
        Assert.False(document.EditorDocument.UndoStack.CanRedo);
    }

    [Fact]
    public async Task Document_BuildsLargePreviewOnlyWhenTheSelectedModeNeedsIt()
    {
        var document = new NoteDocumentState(previewDelay: TimeSpan.Zero);
        document.New();
        document.Content = string.Join('\n', Enumerable.Range(1, 1_000).Select(index => $"## Section {index}\n\nParagraph {index}."));

        Assert.Empty(document.PreviewBlocks);

        var previewChanges = 0;
        document.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NoteDocumentState.PreviewBlocks))
                previewChanges++;
        };
        document.ViewMode = "preview";
        await document.AwaitPreviewAsync();

        Assert.Equal(2_000, document.PreviewBlocks.Count);
        Assert.Equal(1, previewChanges);
        Assert.All(document.PreviewBlocks, block => Assert.NotEqual(typeof(MarkdownBlock), block.GetType()));
    }

    [Fact]
    public void MostRecentlyUpdated_IsIndependentOfAlphabeticalLibraryOrder()
    {
        var list = new NoteListState();
        list.Replace([
            Note("old", "Alpha", "", "2026-07-20T10:00:00Z"),
            Note("new", "Zulu", "", "2026-07-21T10:00:00Z")
        ]);

        Assert.Equal(["Alpha", "Zulu"], list.LibraryNotes.Select(note => note.Title));
        Assert.Equal("new", list.MostRecentlyUpdated()!.Id);
    }

    private static NoteItemVm Note(string id, string title, string content, string updated = "2026-07-21T10:00:00Z")
        => new(id, title, content, false, "2026-07-01T10:00:00Z", updated);
}

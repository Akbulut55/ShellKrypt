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

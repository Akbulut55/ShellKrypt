using ShellKrypt.Desktop.Features.MarkdownNotes;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class MarkdownNotesStateTests
{
    [Fact]
    public void Picker_IsAlphabeticalAndSearchDoesNotChangeDocument()
    {
        var list = new NoteListState();
        list.Replace([
            Note("b", "Zulu", "needle"),
            Note("a", "alpha", "other")
        ]);
        var document = new NoteDocumentState();
        document.Open(list.Notes[0]);

        list.SearchText = "needle";

        Assert.Equal("Zulu", Assert.Single(list.PickerNotes).Title);
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
    public void MostRecentlyUpdated_IsIndependentOfAlphabeticalPickerOrder()
    {
        var list = new NoteListState();
        list.Replace([
            Note("old", "Alpha", "", "2026-07-20T10:00:00Z"),
            Note("new", "Zulu", "", "2026-07-21T10:00:00Z")
        ]);

        Assert.Equal(["Alpha", "Zulu"], list.PickerNotes.Select(note => note.Title));
        Assert.Equal("new", list.MostRecentlyUpdated()!.Id);
    }

    private static NoteItemVm Note(string id, string title, string content, string updated = "2026-07-21T10:00:00Z")
        => new(id, title, content, false, "2026-07-01T10:00:00Z", updated);
}

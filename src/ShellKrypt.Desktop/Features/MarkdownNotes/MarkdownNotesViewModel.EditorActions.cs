using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedNote is null)
        {
            Error = T(_root, "Notes.Error.SelectNoteFirst");
            return;
        }

        var previous = SelectedNote.IsFavorite;
        SelectedNote.IsFavorite = !SelectedNote.IsFavorite;
        RefreshFilteredNotes();
        RefreshNotePicker();
        await SaveAsync();

        if (!string.IsNullOrWhiteSpace(Error))
        {
            SelectedNote.IsFavorite = previous;
            RefreshFilteredNotes();
            RefreshNotePicker();
            return;
        }

        _root.LogActivity(
            "notes",
            previous ? "Markdown note unstarred" : "Markdown note starred",
            $"{SelectedNote.Title} was {(previous ? "removed from" : "added to")} starred notes.",
            "info",
            affectedItem: SelectedNote.Title);
    }

    [RelayCommand]
    private async Task CopyContentAsync()
    {
        Error = string.Empty;

        if (string.IsNullOrWhiteSpace(EditorContent))
        {
            Error = T(_root, "Notes.Error.NothingToCopy");
            return;
        }

        await _root.CopyToClipboardAsync(EditorContent);
        _root.LogActivity("notes", "Markdown note copied", $"Copied markdown for {EditorTitle}.", "info", affectedItem: EditorTitle);
    }
}

using ShellKrypt.Application.Markdown;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    public int VisibleNoteCount => FilteredNotes.Count;
    public string NotesHeader => ActiveFilter switch
    {
        "favorites" => $"STARRED ({VisibleNoteCount})",
        "recent" => $"RECENT ({VisibleNoteCount})",
        _ => $"NOTES ({VisibleNoteCount})"
    };
    public bool HasFilteredNotes => FilteredNotes.Count > 0;
    public bool HasEditor => SelectedNote is not null || IsCreatingNote;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasPreviewContent => PreviewBlocks.Count > 0;
    public bool IsAllFilterActive => ActiveFilter == "all";
    public bool IsFavoritesFilterActive => ActiveFilter == "favorites";
    public bool IsRecentFilterActive => ActiveFilter == "recent";
    public bool IsPreviewMode => ActiveDocumentView == "preview";
    public bool IsSourceMode => ActiveDocumentView == "source";
    public bool CanDeleteSelection => SelectedNote is not null && !IsBusy;
    public bool CanCopySelection => !IsBusy && !string.IsNullOrWhiteSpace(EditorContent);
    public bool CanToggleFavorite => SelectedNote is not null && !IsBusy;
    public bool CanStartEditing => HasEditor && !IsBusy && !IsEditing;
    public bool CanToggleDocumentView => HasEditor && !IsBusy;
    public bool CanSave => IsEditing && !IsBusy && !string.IsNullOrWhiteSpace(EditorTitle);
    public bool ShowEditButton => SelectedNote is not null && !IsEditing;
    public bool ShowSaveButton => IsEditing;
    public string FavoriteToggleLabel => SelectedNote?.IsFavorite == true ? "Unstar" : "Star";
    public string SaveButtonText => SelectedNote is null ? "Create Note" : "Save Note";

    public string PreviewDocumentTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(EditorTitle))
                return EditorTitle.Trim();

            var firstHeading = PreviewBlocks.FirstOrDefault(block =>
                block.Kind is MarkdownBlockKind.Heading1 or MarkdownBlockKind.Heading2 or MarkdownBlockKind.Heading3);

            return !string.IsNullOrWhiteSpace(firstHeading?.Text)
                ? firstHeading.Text
                : "Untitled Markdown Note";
        }
    }

    public string SelectedNoteMeta => SelectedNote is null
        ? IsCreatingNote
            ? "Draft content stays local until you save it into the active vault."
            : "Select a markdown note to inspect or create a new encrypted markdown record."
        : $"Last edited {FormatEditorTimestamp(SelectedNote.UpdatedAtUtc)} | {EditorContent.Length:N0} characters";

    public string EditorStats => $"{EditorContent.Length:N0} characters | {CountWords(EditorContent):N0} words | {CountLines(EditorContent):N0} lines";
    public string EditorStatusLine => string.IsNullOrWhiteSpace(AutoSaveStatus)
        ? EditorStats
        : $"{EditorStats} - {AutoSaveStatus}";

    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? "No starred notes available"
            : "No markdown notes available"
        : "No notes match the current search";

    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? "Star notes from the editor and they will appear here."
            : "Create a new markdown note to start storing encrypted private data in this vault."
        : "Try a different search term or switch back to All.";

    public string DocumentViewToggleText => IsPreviewMode ? "Source" : "Preview";
}

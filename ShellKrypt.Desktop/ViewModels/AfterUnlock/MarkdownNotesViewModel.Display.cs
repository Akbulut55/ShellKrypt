using ShellKrypt.Application.Markdown;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    public int VisibleNoteCount => FilteredNotes.Count;
    public string NotesHeader => ActiveFilter switch
    {
        "favorites" => T(_root, "Notes.Header.Starred", VisibleNoteCount),
        "recent" => T(_root, "Notes.Header.Recent", VisibleNoteCount),
        _ => T(_root, "Notes.Header.Notes", VisibleNoteCount)
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
    public string FavoriteToggleLabel => SelectedNote?.IsFavorite == true ? T(_root, "Notes.Button.Unstar") : T(_root, "Notes.Button.Star");
    public string SaveButtonText => SelectedNote is null ? T(_root, "Notes.Button.CreateNote") : T(_root, "Notes.Button.SaveNote");

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
                : T(_root, "Notes.Untitled");
        }
    }

    public string SelectedNoteMeta => SelectedNote is null
        ? IsCreatingNote
            ? T(_root, "Notes.Meta.Draft")
            : T(_root, "Notes.Meta.Select")
        : T(_root, "Notes.Meta.LastEdited", FormatEditorTimestamp(SelectedNote.UpdatedAtUtc), EditorContent.Length);

    public string EditorStats => T(_root, "Notes.EditorStats", EditorContent.Length, CountWords(EditorContent), CountLines(EditorContent));
    public string EditorStatusLine => string.IsNullOrWhiteSpace(AutoSaveStatus)
        ? EditorStats
        : T(_root, "Notes.EditorStatusLine", EditorStats, AutoSaveStatus);

    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? T(_root, "Notes.Empty.StarredTitle")
            : T(_root, "Notes.Empty.NoneTitle")
        : T(_root, "Notes.Empty.NoMatchTitle");

    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? T(_root, "Notes.Empty.StarredSubtitle")
            : T(_root, "Notes.Empty.NoneSubtitle")
        : T(_root, "Notes.Empty.NoMatchSubtitle");

    public string DocumentViewToggleText => IsPreviewMode ? T(_root, "Notes.Button.Source") : T(_root, "Notes.Button.Preview");
}

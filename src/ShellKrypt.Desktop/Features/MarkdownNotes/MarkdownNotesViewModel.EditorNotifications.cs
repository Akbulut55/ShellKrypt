using System;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesViewModel
{
    private void NotifyEditorStateChanged()
    {
        OnPropertyChanged(nameof(SelectedNoteMeta));
        OnPropertyChanged(nameof(EditorStats));
        OnPropertyChanged(nameof(EditorStatusLine));
        OnPropertyChanged(nameof(HasEditor));
        OnPropertyChanged(nameof(CanDeleteSelection));
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanToggleFavorite));
        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanToggleDocumentView));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancelEditorChanges));
        OnPropertyChanged(nameof(FavoriteToggleLabel));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(ShowHeaderCommitButtons));
        OnPropertyChanged(nameof(ShowHeaderCreateButton));
        OnPropertyChanged(nameof(DocumentViewToggleText));
        OnPropertyChanged(nameof(PreviewDocumentTitle));
        OnPropertyChanged(nameof(SelectedNoteTitleDisplay));
        OnPropertyChanged(nameof(HeaderSaveStatus));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(IsEditorOnlyMode));
        OnPropertyChanged(nameof(IsPreviewOnlyMode));
        OnPropertyChanged(nameof(IsEditorPaneVisible));
        OnPropertyChanged(nameof(IsPreviewPaneVisible));
        OnPropertyChanged(nameof(HasNotePickerFavorites));
        OnPropertyChanged(nameof(HasNotePickerRecent));
        OnPropertyChanged(nameof(HasNotePickerAll));
        RefreshPreviewContent();
    }

    private static string FormatEditorTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return value;

        return timestamp.ToLocalTime().ToString("HH:mm | MMM dd");
    }
}

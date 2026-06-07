using System;

namespace ShellKrypt.Desktop.ViewModels;

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
        OnPropertyChanged(nameof(FavoriteToggleLabel));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(DocumentViewToggleText));
        OnPropertyChanged(nameof(PreviewDocumentTitle));
        RefreshPreviewContent();
    }

    private static string FormatEditorTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return value;

        return timestamp.ToLocalTime().ToString("HH:mm | MMM dd");
    }
}

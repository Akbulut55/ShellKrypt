namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    partial void OnSelectedNoteChanged(NoteItemVm? value)
    {
        CancelPendingAutoSave();
        UpdateSelectionState();

        _suppressAutoSave = true;
        try
        {
            if (value is null)
            {
                if (!IsCreatingNote)
                {
                    EditorTitle = string.Empty;
                    EditorContent = string.Empty;
                    IsEditing = false;
                    ActiveDocumentView = "preview";
                }
            }
            else
            {
                IsCreatingNote = false;
                IsEditing = false;
                ActiveDocumentView = "preview";
                EditorTitle = value.Title;
                EditorContent = value.Content;
            }
        }
        finally
        {
            _suppressAutoSave = false;
        }

        AutoSaveStatus = string.Empty;
        NotifyEditorStateChanged();
    }

    partial void OnEditorTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(PreviewDocumentTitle));
        ScheduleAutoSave();
    }

    partial void OnEditorContentChanged(string value)
    {
        OnPropertyChanged(nameof(EditorStats));
        OnPropertyChanged(nameof(EditorStatusLine));
        OnPropertyChanged(nameof(SelectedNoteMeta));
        OnPropertyChanged(nameof(CanCopySelection));
        RefreshPreviewContent();
        ScheduleAutoSave();
    }

    partial void OnNoteCountChanged(int value)
    {
        OnPropertyChanged(nameof(NotesHeader));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredNotes(false);
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    partial void OnActiveFilterChanged(string value)
    {
        RefreshFilteredNotes(false);
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsFavoritesFilterActive));
        OnPropertyChanged(nameof(IsRecentFilterActive));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    partial void OnErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnAutoSaveStatusChanged(string value)
    {
        OnPropertyChanged(nameof(EditorStatusLine));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDeleteSelection));
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanToggleFavorite));
        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanToggleDocumentView));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsCreatingNoteChanged(bool value)
    {
        NotifyEditorStateChanged();
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value)
            CancelPendingAutoSave();

        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
    }

    partial void OnActiveDocumentViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(DocumentViewToggleText));
    }
}

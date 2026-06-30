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
                    ActiveDocumentView = "split";
                }
            }
            else
            {
                IsCreatingNote = false;
                IsEditing = false;
                if (ActiveDocumentView is not ("split" or "editor" or "preview"))
                    ActiveDocumentView = "split";
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
        if (!_suppressAutoSave && HasEditor && !IsEditing)
            IsEditing = true;

        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancelEditorChanges));
        OnPropertyChanged(nameof(ShowHeaderCommitButtons));
        OnPropertyChanged(nameof(ShowHeaderCreateButton));
        OnPropertyChanged(nameof(PreviewDocumentTitle));
        OnPropertyChanged(nameof(SelectedNoteTitleDisplay));
        ScheduleAutoSave();
    }

    partial void OnEditorContentChanged(string value)
    {
        if (!_suppressAutoSave && HasEditor && !IsEditing)
            IsEditing = true;

        OnPropertyChanged(nameof(EditorStats));
        OnPropertyChanged(nameof(EditorStatusLine));
        OnPropertyChanged(nameof(SelectedNoteMeta));
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancelEditorChanges));
        OnPropertyChanged(nameof(ShowHeaderCommitButtons));
        OnPropertyChanged(nameof(ShowHeaderCreateButton));
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
        RefreshNotePicker();
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    partial void OnNotePickerSearchTextChanged(string value)
    {
        RefreshNotePicker();
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
        OnPropertyChanged(nameof(HeaderSaveStatus));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDeleteSelection));
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanToggleFavorite));
        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanToggleDocumentView));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancelEditorChanges));
        OnPropertyChanged(nameof(ShowHeaderCommitButtons));
        OnPropertyChanged(nameof(ShowHeaderCreateButton));
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
        OnPropertyChanged(nameof(CanCancelEditorChanges));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(ShowHeaderCommitButtons));
        OnPropertyChanged(nameof(ShowHeaderCreateButton));
    }

    partial void OnActiveDocumentViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(IsEditorOnlyMode));
        OnPropertyChanged(nameof(IsPreviewOnlyMode));
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(IsEditorPaneVisible));
        OnPropertyChanged(nameof(IsPreviewPaneVisible));
        OnPropertyChanged(nameof(DocumentViewToggleText));
    }
}

using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private void BeginEditing()
    {
        if (!HasEditor)
            return;

        IsEditing = true;
        ActiveDocumentView = "editor";
    }

    [RelayCommand]
    private void NewNote()
    {
        CancelPendingAutoSave();
        Error = string.Empty;
        IsCreatingNote = true;
        IsEditing = true;
        ActiveDocumentView = "editor";
        IsNotePickerOpen = false;
        SelectedNote = null;
        _suppressAutoSave = true;
        try
        {
            EditorTitle = string.Empty;
            EditorContent = string.Empty;
        }
        finally
        {
            _suppressAutoSave = false;
        }
        AutoSaveStatus = string.Empty;
        NotifyEditorStateChanged();
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void CancelEditing()
    {
        CancelPendingAutoSave();
        Error = string.Empty;

        if (IsCreatingNote)
        {
            _suppressAutoSave = true;
            try
            {
                EditorTitle = string.Empty;
                EditorContent = string.Empty;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            IsCreatingNote = false;
            IsEditing = false;
            ActiveDocumentView = "split";

            if (SelectedNote is null && FilteredNotes.FirstOrDefault() is { } fallback)
                SelectedNote = fallback;
            else
                NotifyEditorStateChanged();

            return;
        }

        if (SelectedNote is null)
            return;

        _suppressAutoSave = true;
        try
        {
            EditorTitle = SelectedNote.Title;
            EditorContent = SelectedNote.Content;
        }
        finally
        {
            _suppressAutoSave = false;
        }

        IsEditing = false;
        ActiveDocumentView = "split";
        AutoSaveStatus = string.Empty;
        NotifyEditorStateChanged();
    }
}

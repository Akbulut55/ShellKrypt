using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private void BeginEditing()
    {
        if (!HasEditor)
            return;

        IsEditing = true;
        ActiveDocumentView = "source";
    }

    [RelayCommand]
    private void NewNote()
    {
        CancelPendingAutoSave();
        Error = string.Empty;
        IsCreatingNote = true;
        IsEditing = true;
        ActiveDocumentView = "source";
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
}

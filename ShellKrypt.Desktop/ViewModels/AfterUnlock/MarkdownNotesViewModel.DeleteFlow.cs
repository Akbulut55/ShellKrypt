using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private async Task DeleteAsync()
    {
        CancelPendingAutoSave();
        Error = string.Empty;

        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }
        if (SelectedNote is null) { Error = T(_root, "Notes.Error.SelectNoteFirst"); return; }

        IsBusy = true;
        try
        {
            var deletedTitle = SelectedNote.Title;
            var deletedId = SelectedNote.Id;
            await _noteService.DeleteAsync(_root.VaultPath, deletedId);
            _root.LogActivity("notes", "Markdown note deleted", $"Deleted {deletedTitle}.", "warning", affectedItem: deletedTitle);

            Notes.Remove(SelectedNote);
            RefreshFilteredNotes(false);
            RefreshNotePicker();
            IsEditing = false;
            ActiveDocumentView = "split";

            if (FilteredNotes.Count > 0)
            {
                var replacement = FilteredNotes.FirstOrDefault();
                if (replacement is not null)
                    SelectedNote = replacement;
            }
            else if (SelectedNote?.Id == deletedId)
            {
                SelectedNote = null;
            }

            await _refreshAllItemsAsync(null);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private async Task SaveAsync()
    {
        CancelPendingAutoSave();
        await SaveCoreAsync(keepEditing: false, logActivity: true, isAutoSave: false);
    }

    private async Task SaveCoreAsync(bool keepEditing, bool logActivity, bool isAutoSave, CancellationToken ct = default)
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            if (!isAutoSave)
                Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            if (isAutoSave)
                AutoSaveStatus = T(_root, "Notes.AutoSave.TitleRequired");
            else
                Error = T(_root, "Validation.TitleRequired");
            return;
        }

        if (isAutoSave && !HasUnsavedEditorChanges())
            return;

        IsBusy = true;
        if (isAutoSave)
            AutoSaveStatus = T(_root, "Notes.AutoSave.Saving");

        var titleSnapshot = EditorTitle;
        var contentSnapshot = EditorContent;

        try
        {
            if (SelectedNote is null)
            {
                var entry = await _noteService.AddAsync(
                    _root.VaultPath,
                    _root.VaultKey,
                    new NoteInput(titleSnapshot, contentSnapshot, Favorite: false),
                    ct);

                var vm = new NoteItemVm(entry.Id, entry.Title, entry.Content, entry.Favorite, entry.CreatedAtUtc, entry.UpdatedAtUtc);
                Notes.Insert(0, vm);
                IsCreatingNote = false;
                SelectedNote = vm;
                if (keepEditing)
                {
                    IsEditing = true;
                    ActiveDocumentView = "editor";
                    EditorTitle = titleSnapshot;
                    EditorContent = contentSnapshot;
                }
                else
                {
                    IsEditing = false;
                    ActiveDocumentView = "preview";
                }

                await _refreshAllItemsAsync(entry.Id);
                if (logActivity)
                    _root.LogActivity("notes", "Markdown note created", $"Saved {entry.Title}.", "success", affectedItem: entry.Title);
            }
            else
            {
                var entry = await _noteService.UpdateAsync(
                    _root.VaultPath,
                    _root.VaultKey,
                    SelectedNote.Id,
                    SelectedNote.CreatedAtUtc,
                    new NoteInput(titleSnapshot, contentSnapshot, SelectedNote.IsFavorite),
                    ct);

                SelectedNote.Apply(entry);
                if (!keepEditing)
                {
                    IsEditing = false;
                    ActiveDocumentView = "preview";
                }

                await _refreshAllItemsAsync(entry.Id);
                if (logActivity)
                    _root.LogActivity("notes", "Markdown note updated", $"Updated {entry.Title}.", "info", affectedItem: entry.Title);
            }

            if (isAutoSave)
                AutoSaveStatus = T(_root, "Notes.AutoSave.SavedAt", DateTime.Now.ToString("HH:mm:ss"));

            RefreshFilteredNotes();
            RefreshNotePicker();
            OnPropertyChanged(nameof(SelectedNoteMeta));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(SelectedNoteTitleDisplay));
            OnPropertyChanged(nameof(ShowHeaderCommitButtons));
            OnPropertyChanged(nameof(ShowHeaderCreateButton));
        }
        catch (OperationCanceledException) when (isAutoSave)
        {
            AutoSaveStatus = string.Empty;
        }
        catch (Exception ex)
        {
            if (isAutoSave)
                AutoSaveStatus = T(_root, "Notes.AutoSave.Failed", ex.Message);
            else
                Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

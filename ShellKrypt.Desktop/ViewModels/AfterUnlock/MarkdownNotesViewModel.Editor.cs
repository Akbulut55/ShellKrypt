using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Threading;
using System.Threading.Tasks;

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
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedNote is null)
        {
            Error = "Select a note first.";
            return;
        }

        var previous = SelectedNote.IsFavorite;
        SelectedNote.IsFavorite = !SelectedNote.IsFavorite;
        RefreshFilteredNotes();
        await SaveAsync();

        if (!string.IsNullOrWhiteSpace(Error))
        {
            SelectedNote.IsFavorite = previous;
            RefreshFilteredNotes();
            return;
        }

        _root.LogActivity(
            "notes",
            previous ? "Markdown note unstarred" : "Markdown note starred",
            $"{SelectedNote.Title} was {(previous ? "removed from" : "added to")} starred notes.",
            "info",
            affectedItem: SelectedNote.Title);
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

    [RelayCommand]
    private async Task CopyContentAsync()
    {
        Error = string.Empty;

        if (string.IsNullOrWhiteSpace(EditorContent))
        {
            Error = "Nothing to copy yet.";
            return;
        }

        await _root.CopyToClipboardAsync(EditorContent);
        _root.LogActivity("notes", "Markdown note copied", $"Copied markdown for {EditorTitle}.", "info", affectedItem: EditorTitle);
    }

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
                Error = "No vault selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            if (isAutoSave)
                AutoSaveStatus = "Autosave paused: title required.";
            else
                Error = "Title is required.";
            return;
        }

        if (isAutoSave && !HasUnsavedEditorChanges())
            return;

        IsBusy = true;
        if (isAutoSave)
            AutoSaveStatus = "Autosaving...";

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
                    ActiveDocumentView = "source";
                    EditorTitle = titleSnapshot;
                    EditorContent = contentSnapshot;
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
                AutoSaveStatus = $"Autosaved at {DateTime.Now:HH:mm:ss}";

            RefreshFilteredNotes();
            OnPropertyChanged(nameof(SelectedNoteMeta));
            OnPropertyChanged(nameof(SaveButtonText));
        }
        catch (OperationCanceledException) when (isAutoSave)
        {
            AutoSaveStatus = string.Empty;
        }
        catch (Exception ex)
        {
            if (isAutoSave)
                AutoSaveStatus = "Autosave failed.";
            else
                Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

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

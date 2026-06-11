using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    private void ScheduleAutoSave()
    {
        if (_suppressAutoSave || !IsEditing)
            return;

        CancelPendingAutoSave(clearStatus: false);

        if (!HasUnsavedEditorChanges())
        {
            AutoSaveStatus = string.Empty;
            return;
        }

        AutoSaveStatus = T(_root, "Notes.AutoSave.Pending");
        _autoSaveCts = new CancellationTokenSource();
        _ = DebouncedAutoSaveAsync(_autoSaveCts.Token);
    }

    private async Task DebouncedAutoSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(AutoSaveDelay, ct);

            while (IsBusy)
                await Task.Delay(250, ct);

            if (!IsEditing || !HasUnsavedEditorChanges())
                return;

            await SaveCoreAsync(keepEditing: true, logActivity: false, isAutoSave: true, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPendingAutoSave(bool clearStatus = true)
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = null;

        if (clearStatus)
            AutoSaveStatus = string.Empty;
    }

    private bool HasUnsavedEditorChanges()
    {
        if (!IsCreatingNote && SelectedNote is null)
            return false;

        if (string.IsNullOrWhiteSpace(EditorTitle))
            return false;

        if (SelectedNote is null)
            return IsCreatingNote && (!string.IsNullOrWhiteSpace(EditorTitle) || !string.IsNullOrWhiteSpace(EditorContent));

        return !string.Equals(EditorTitle.Trim(), SelectedNote.Title, StringComparison.Ordinal) ||
               !string.Equals(EditorContent, SelectedNote.Content, StringComparison.Ordinal);
    }
}

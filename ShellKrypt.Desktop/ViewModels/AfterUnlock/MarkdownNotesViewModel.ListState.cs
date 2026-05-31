using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Markdown;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private void ShowAll()
    {
        ActiveFilter = "all";
    }

    [RelayCommand]
    private void ShowFavorites()
    {
        ActiveFilter = "favorites";
    }

    [RelayCommand]
    private void ShowRecent()
    {
        ActiveFilter = "recent";
    }

    private void UpdateNoteCount()
    {
        NoteCount = Notes.Count;
    }

    private void RefreshFilteredNotes(bool preserveSelection = true)
    {
        var selectedId = preserveSelection ? SelectedNote?.Id : null;

        var items = Notes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            items = items.Where(note =>
                note.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                SimpleMarkdown.ToPlainText(note.Content).Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        items = ActiveFilter switch
        {
            "favorites" => items.Where(note => note.IsFavorite),
            "recent" => items.OrderByDescending(note => ParseTimestamp(note.UpdatedAtUtc)).Take(10),
            _ => items.OrderByDescending(note => ParseTimestamp(note.UpdatedAtUtc))
        };

        var snapshot = items.ToList();

        FilteredNotes.Clear();
        foreach (var note in snapshot)
            FilteredNotes.Add(note);

        if (snapshot.Count == 0)
        {
            if (SelectedNote is not null)
                SelectedNote = null;
            return;
        }

        if (selectedId is not null)
        {
            var existing = snapshot.FirstOrDefault(note => note.Id == selectedId);
            if (existing is not null)
            {
                if (!ReferenceEquals(SelectedNote, existing))
                    SelectedNote = existing;
                return;
            }
        }

        if (IsCreatingNote)
        {
            if (SelectedNote is not null)
                SelectedNote = null;
            return;
        }

        if (SelectedNote is null || snapshot.All(note => note.Id != SelectedNote.Id))
            SelectedNote = snapshot[0];

        UpdateSelectionState();
    }

    public void SelectNote(NoteItemVm? note)
    {
        if (note is null)
            return;

        if (!ReferenceEquals(SelectedNote, note))
            SelectedNote = note;
    }

    private void UpdateSelectionState()
    {
        foreach (var note in FilteredNotes)
            note.IsSelected = ReferenceEquals(note, SelectedNote);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;
    }

    private async Task LoadAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        IsBusy = true;
        try
        {
            Notes.Clear();

            var notes = await _noteService.ListAsync(_root.VaultPath, _root.VaultKey);

            foreach (var note in notes)
                Notes.Add(new NoteItemVm(note.Id, note.Title, note.Content, note.Favorite, note.CreatedAtUtc, note.UpdatedAtUtc));

            RefreshFilteredNotes();
            NotifyEditorStateChanged();
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

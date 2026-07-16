using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Markdown;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

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

    [RelayCommand]
    private void ToggleNotePicker()
    {
        RefreshNotePicker();
        IsNotePickerOpen = true;
    }

    [RelayCommand]
    private void CloseNotePicker()
    {
        IsNotePickerOpen = false;
    }

    [RelayCommand]
    private void SelectNoteFromPicker(NoteItemVm? note)
    {
        if (note is null)
            return;

        SelectNote(note);
        IsNotePickerOpen = false;
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

    private void RefreshNotePicker()
    {
        var items = Notes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(NotePickerSearchText))
        {
            var query = NotePickerSearchText.Trim();
            items = items.Where(note =>
                note.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                SimpleMarkdown.ToPlainText(note.Content).Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var snapshot = items
            .OrderByDescending(note => ParseTimestamp(note.UpdatedAtUtc))
            .ToList();

        RebuildPickerCollection(NotePickerFavorites, snapshot.Where(note => note.IsFavorite));
        RebuildPickerCollection(NotePickerRecent, snapshot.Take(6));
        RebuildPickerCollection(NotePickerAll, snapshot.OrderBy(note => note.Title, StringComparer.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(HasNotePickerFavorites));
        OnPropertyChanged(nameof(HasNotePickerRecent));
        OnPropertyChanged(nameof(HasNotePickerAll));
    }

    private static void RebuildPickerCollection(ObservableCollection<NoteItemVm> target, IEnumerable<NoteItemVm> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
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

        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }

        IsBusy = true;
        try
        {
            Notes.Clear();

            var notes = await _noteService.ListAsync(_root.VaultPath, _root.VaultKey);

            foreach (var note in notes)
                Notes.Add(new NoteItemVm(note.Id, note.Title, note.Content, note.Favorite, note.CreatedAtUtc, note.UpdatedAtUtc));

            RefreshFilteredNotes();
            RefreshNotePicker();
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

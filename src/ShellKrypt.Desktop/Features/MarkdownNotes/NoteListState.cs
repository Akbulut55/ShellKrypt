using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Markdown;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public sealed partial class NoteListState : ViewModelBase
{
    public ObservableCollection<NoteItemVm> Notes { get; } = new();
    public ObservableCollection<NoteItemVm> LibraryNotes { get; } = new();

    [ObservableProperty] private string searchText = "";

    public int Count => Notes.Count;
    public bool HasResults => LibraryNotes.Count > 0;

    partial void OnSearchTextChanged(string value) => RefreshLibrary();

    public void Replace(IEnumerable<NoteItemVm> notes)
    {
        Notes.Clear();
        foreach (var note in notes)
            Notes.Add(note);
        RefreshLibrary();
    }

    public void RefreshLibrary()
    {
        IEnumerable<NoteItemVm> query = Notes;
        var search = SearchText.Trim();
        if (search.Length > 0)
        {
            query = query.Where(note =>
                note.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                SimpleMarkdown.ToPlainText(note.Content).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        LibraryNotes.Clear();
        foreach (var note in query.OrderBy(note => note.Title, StringComparer.OrdinalIgnoreCase))
            LibraryNotes.Add(note);

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasResults));
    }

    public NoteItemVm? MostRecentlyUpdated()
        => Notes.OrderByDescending(note => ParseTimestamp(note.UpdatedAtUtc)).FirstOrDefault();

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : DateTimeOffset.MinValue;
}

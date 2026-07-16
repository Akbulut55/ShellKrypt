using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Core.Items;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesViewModel : ViewModelBase
{
    private readonly MarkdownNotesRuntime _root;
    private readonly INoteService _noteService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private CancellationTokenSource? _autoSaveCts;
    private bool _suppressAutoSave;

    public ObservableCollection<NoteItemVm> Notes { get; } = new();
    public ObservableCollection<NoteItemVm> FilteredNotes { get; } = new();
    public ObservableCollection<NoteItemVm> NotePickerFavorites { get; } = new();
    public ObservableCollection<NoteItemVm> NotePickerRecent { get; } = new();
    public ObservableCollection<NoteItemVm> NotePickerAll { get; } = new();
    public ObservableCollection<MarkdownBlock> PreviewBlocks { get; } = new();

    [ObservableProperty] private NoteItemVm? selectedNote;
    [ObservableProperty] private string editorTitle = "";
    [ObservableProperty] private string editorContent = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private int noteCount;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeFilter = "all";
    [ObservableProperty] private bool isCreatingNote;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string activeDocumentView = "preview";
    [ObservableProperty] private bool isNotePickerOpen;
    [ObservableProperty] private string notePickerSearchText = "";
    [ObservableProperty] private string autoSaveStatus = "";

    public MarkdownNotesViewModel(MarkdownNotesRuntime root, INoteService noteService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _noteService = noteService;
        _refreshAllItemsAsync = refreshAllItemsAsync;

        Notes.CollectionChanged += (_, __) =>
        {
            UpdateNoteCount();
            RefreshFilteredNotes();
            RefreshNotePicker();
        };

        FilteredNotes.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(VisibleNoteCount));
            OnPropertyChanged(nameof(NotesHeader));
            OnPropertyChanged(nameof(HasFilteredNotes));
            OnPropertyChanged(nameof(EmptyStateTitle));
            OnPropertyChanged(nameof(EmptyStateSubtitle));
        };

        PreviewBlocks.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasPreviewContent));
            OnPropertyChanged(nameof(PreviewDocumentTitle));
        };

        UpdateNoteCount();
        RefreshFilteredNotes(false);
        _ = LoadAsync();
    }

    public override void RefreshLocalization()
    {
        NotifyEditorStateChanged();
        NotifyLocalized(
            nameof(NotesHeader),
            nameof(EmptyStateTitle),
            nameof(EmptyStateSubtitle),
            nameof(SelectedNoteTitleDisplay),
            nameof(HeaderSaveStatus),
            nameof(DocumentViewToggleText));
    }
}

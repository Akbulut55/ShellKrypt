using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Core.Items;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel : ViewModelBase
{
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(3);

    private readonly MainWindowViewModel _root;
    private readonly INoteService _noteService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private CancellationTokenSource? _autoSaveCts;
    private bool _suppressAutoSave;

    public ObservableCollection<NoteItemVm> Notes { get; } = new();
    public ObservableCollection<NoteItemVm> FilteredNotes { get; } = new();
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
    [ObservableProperty] private string autoSaveStatus = "";

    public MarkdownNotesViewModel(MainWindowViewModel root, INoteService noteService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _noteService = noteService;
        _refreshAllItemsAsync = refreshAllItemsAsync;

        Notes.CollectionChanged += (_, __) =>
        {
            UpdateNoteCount();
            RefreshFilteredNotes();
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

    public int VisibleNoteCount => FilteredNotes.Count;
    public string NotesHeader => ActiveFilter switch
    {
        "favorites" => $"STARRED ({VisibleNoteCount})",
        "recent" => $"RECENT ({VisibleNoteCount})",
        _ => $"NOTES ({VisibleNoteCount})"
    };
    public bool HasFilteredNotes => FilteredNotes.Count > 0;
    public bool HasEditor => SelectedNote is not null || IsCreatingNote;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasPreviewContent => PreviewBlocks.Count > 0;
    public bool IsAllFilterActive => ActiveFilter == "all";
    public bool IsFavoritesFilterActive => ActiveFilter == "favorites";
    public bool IsRecentFilterActive => ActiveFilter == "recent";
    public bool IsPreviewMode => ActiveDocumentView == "preview";
    public bool IsSourceMode => ActiveDocumentView == "source";
    public bool CanDeleteSelection => SelectedNote is not null && !IsBusy;
    public bool CanCopySelection => !IsBusy && !string.IsNullOrWhiteSpace(EditorContent);
    public bool CanToggleFavorite => SelectedNote is not null && !IsBusy;
    public bool CanStartEditing => HasEditor && !IsBusy && !IsEditing;
    public bool CanToggleDocumentView => HasEditor && !IsBusy;
    public bool CanSave => IsEditing && !IsBusy && !string.IsNullOrWhiteSpace(EditorTitle);
    public bool ShowEditButton => SelectedNote is not null && !IsEditing;
    public bool ShowSaveButton => IsEditing;
    public string FavoriteToggleLabel => SelectedNote?.IsFavorite == true ? "Unstar" : "Star";
    public string SaveButtonText => SelectedNote is null ? "Create Note" : "Save Note";

    public string PreviewDocumentTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(EditorTitle))
                return EditorTitle.Trim();

            var firstHeading = PreviewBlocks.FirstOrDefault(block =>
                block.Kind is MarkdownBlockKind.Heading1 or MarkdownBlockKind.Heading2 or MarkdownBlockKind.Heading3);

            return !string.IsNullOrWhiteSpace(firstHeading?.Text)
                ? firstHeading.Text
                : "Untitled Markdown Note";
        }
    }

    public string SelectedNoteMeta => SelectedNote is null
        ? IsCreatingNote
            ? "Draft content stays local until you save it into the active vault."
            : "Select a markdown note to inspect or create a new encrypted markdown record."
        : $"Last edited {FormatEditorTimestamp(SelectedNote.UpdatedAtUtc)} | {EditorContent.Length:N0} characters";

    public string EditorStats => $"{EditorContent.Length:N0} characters | {CountWords(EditorContent):N0} words | {CountLines(EditorContent):N0} lines";
    public string EditorStatusLine => string.IsNullOrWhiteSpace(AutoSaveStatus)
        ? EditorStats
        : $"{EditorStats} - {AutoSaveStatus}";

    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? "No starred notes available"
            : "No markdown notes available"
        : "No notes match the current search";

    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? ActiveFilter == "favorites"
            ? "Star notes from the editor and they will appear here."
            : "Create a new markdown note to start storing encrypted private data in this vault."
        : "Try a different search term or switch back to All.";

    public string DocumentViewToggleText => IsPreviewMode ? "Source" : "Preview";

    partial void OnSelectedNoteChanged(NoteItemVm? value)
    {
        CancelPendingAutoSave();
        UpdateSelectionState();

        _suppressAutoSave = true;
        try
        {
            if (value is null)
            {
                if (!IsCreatingNote)
                {
                    EditorTitle = string.Empty;
                    EditorContent = string.Empty;
                    IsEditing = false;
                    ActiveDocumentView = "preview";
                }
            }
            else
            {
                IsCreatingNote = false;
                IsEditing = false;
                ActiveDocumentView = "preview";
                EditorTitle = value.Title;
                EditorContent = value.Content;
            }
        }
        finally
        {
            _suppressAutoSave = false;
        }

        AutoSaveStatus = string.Empty;
        NotifyEditorStateChanged();
    }

    partial void OnEditorTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(PreviewDocumentTitle));
        ScheduleAutoSave();
    }

    partial void OnEditorContentChanged(string value)
    {
        OnPropertyChanged(nameof(EditorStats));
        OnPropertyChanged(nameof(EditorStatusLine));
        OnPropertyChanged(nameof(SelectedNoteMeta));
        OnPropertyChanged(nameof(CanCopySelection));
        RefreshPreviewContent();
        ScheduleAutoSave();
    }

    partial void OnNoteCountChanged(int value)
    {
        OnPropertyChanged(nameof(NotesHeader));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredNotes(false);
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    partial void OnActiveFilterChanged(string value)
    {
        RefreshFilteredNotes(false);
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsFavoritesFilterActive));
        OnPropertyChanged(nameof(IsRecentFilterActive));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    partial void OnErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnAutoSaveStatusChanged(string value)
    {
        OnPropertyChanged(nameof(EditorStatusLine));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDeleteSelection));
        OnPropertyChanged(nameof(CanCopySelection));
        OnPropertyChanged(nameof(CanToggleFavorite));
        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanToggleDocumentView));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsCreatingNoteChanged(bool value)
    {
        NotifyEditorStateChanged();
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value)
            CancelPendingAutoSave();

        OnPropertyChanged(nameof(CanStartEditing));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
    }

    partial void OnActiveDocumentViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(DocumentViewToggleText));
    }
}

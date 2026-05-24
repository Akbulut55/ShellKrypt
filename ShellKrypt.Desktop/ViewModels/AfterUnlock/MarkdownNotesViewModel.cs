using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Core.Items;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NoteItemVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string content;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private bool isSelected;

    public NoteItemVm(string id, string title, string content, bool favorite, string createdAtUtc, string updatedAtUtc)
    {
        Id = id;
        Title = title;
        Content = content;
        IsFavorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string FavoriteGlyph => IsFavorite ? "STAR" : string.Empty;

    public string PreviewText
    {
        get
        {
            var plainText = SimpleMarkdown.ToPlainText(Content);
            return string.IsNullOrWhiteSpace(plainText)
                ? "No content yet."
                : plainText;
        }
    }

    public string UpdatedDisplay => FormatRelativeTimestamp(UpdatedAtUtc);
    public string StatusLabel => IsFavorite ? "Starred" : "Markdown";

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteGlyph));
        OnPropertyChanged(nameof(StatusLabel));
    }

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewText));
    }

    public void TouchUpdated()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        OnPropertyChanged(nameof(UpdatedAtUtc));
        OnPropertyChanged(nameof(UpdatedDisplay));
    }

    public void Apply(NoteEntry entry)
    {
        Title = entry.Title;
        Content = entry.Content;
        IsFavorite = entry.Favorite;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        OnPropertyChanged(nameof(UpdatedAtUtc));
        OnPropertyChanged(nameof(UpdatedDisplay));
    }

    private static string FormatRelativeTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return value;

        var delta = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();

        if (delta.TotalMinutes < 1)
            return "just now";

        if (delta.TotalHours < 1)
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";

        if (delta.TotalDays < 1)
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";

        if (delta.TotalDays < 7)
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return timestamp.ToLocalTime().ToString("MMM dd");
    }
}

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
    private void ToggleDocumentView()
    {
        if (!HasEditor)
            return;

        if (IsPreviewMode)
        {
            if (!IsEditing)
                IsEditing = true;

            ActiveDocumentView = "source";
        }
        else
        {
            ActiveDocumentView = "preview";
        }
    }

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

    [RelayCommand]
    private async Task DeleteAsync()
    {
        CancelPendingAutoSave();
        Error = string.Empty;

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (SelectedNote is null) { Error = "Select a note first."; return; }

        IsBusy = true;
        try
        {
            var deletedTitle = SelectedNote.Title;
            var deletedId = SelectedNote.Id;
            await _noteService.DeleteAsync(_root.VaultPath, deletedId);
            _root.LogActivity("notes", "Markdown note deleted", $"Deleted {deletedTitle}.", "warning", affectedItem: deletedTitle);

            Notes.Remove(SelectedNote);
            RefreshFilteredNotes(false);
            IsEditing = false;
            ActiveDocumentView = "preview";

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

        AutoSaveStatus = "Autosave pending...";
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

    private void UpdateSelectionState()
    {
        foreach (var note in FilteredNotes)
            note.IsSelected = ReferenceEquals(note, SelectedNote);
    }

    private void RefreshPreviewContent()
    {
        PreviewBlocks.Clear();
        foreach (var block in SimpleMarkdown.Parse(EditorContent))
            PreviewBlocks.Add(block);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;
    }

    private static string FormatEditorTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return value;

        return timestamp.ToLocalTime().ToString("HH:mm | MMM dd");
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Length;
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

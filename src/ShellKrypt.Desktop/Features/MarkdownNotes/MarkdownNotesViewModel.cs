using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Application.Notes;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesViewModel : ViewModelBase
{
    private readonly MarkdownNotesRuntime _root;
    private readonly INoteService _noteService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly NoteAutoSaveController _autoSave;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private bool _active;
    private bool _loaded;
    private int _skippedCorruptEntries;

    public NoteListState List { get; } = new();
    public NoteDocumentState Document { get; } = new();

    [ObservableProperty] private NoteItemVm? selectedNote;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string warning = "";
    [ObservableProperty] private string autoSaveStatus = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isNotePickerOpen;

    public MarkdownNotesViewModel(
        MarkdownNotesRuntime root,
        INoteService noteService,
        Func<string?, Task> refreshAllItemsAsync,
        TimeProvider? timeProvider = null)
    {
        _root = root;
        _noteService = noteService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        var clock = timeProvider ?? TimeProvider.System;
        _timeProvider = clock;
        _autoSave = new NoteAutoSaveController(clock, () => TimeSpan.FromSeconds(Math.Max(1, _root.MarkdownAutoSaveSeconds)));
        Document.DraftChanged += (_, _) => OnDraftChanged();
        Document.PropertyChanged += (_, _) => NotifyDocumentState();
        List.PropertyChanged += (_, _) => NotifyListState();
        List.PickerNotes.CollectionChanged += (_, _) => NotifyListState();
    }

    public ObservableCollection<NoteItemVm> NotePickerAll => List.PickerNotes;
    public ObservableCollection<MarkdownBlock> PreviewBlocks => Document.PreviewBlocks;
    public string EditorTitle { get => Document.Title; set => Document.Title = value; }
    public string EditorContent { get => Document.Content; set => Document.Content = value; }
    public string NotePickerSearchText { get => List.SearchText; set => List.SearchText = value; }
    public int NoteCount => List.Count;
    public bool HasNotePickerAll => List.HasResults;
    public bool HasEditor => Document.HasDocument;
    public bool IsCreatingNote => Document.IsCreating;
    public bool IsEditing => Document.IsEditing;
    public bool HasPreviewContent => Document.HasPreview;
    public bool HasError => Error.Length > 0;
    public bool HasWarning => Warning.Length > 0;
    public bool IsSplitMode => Document.ViewMode == "split";
    public bool IsEditorOnlyMode => Document.ViewMode == "editor";
    public bool IsPreviewOnlyMode => Document.ViewMode == "preview";
    public bool CanSave => Document.IsEditing && !IsBusy && !string.IsNullOrWhiteSpace(Document.Title);
    public bool CanManageSelection => SelectedNote is not null && !IsBusy;
    public bool CanCopyContent => !IsBusy && !string.IsNullOrWhiteSpace(Document.Content);
    public string FavoriteToggleLabel => SelectedNote?.IsFavorite == true
        ? T(_root, "Notes.Button.Unstar")
        : T(_root, "Notes.Button.Star");
    public bool ShowHeaderCommitButtons => Document.IsDirty;
    public bool ShowHeaderCreateButton => !Document.IsDirty;
    public string SelectedNoteTitleDisplay => HasEditor
        ? string.IsNullOrWhiteSpace(Document.Title) ? T(_root, "Notes.Untitled") : Document.Title.Trim()
        : T(_root, "Notes.Picker.SelectNote");
    public string SelectedNoteMeta => Document.IsCreating
        ? T(_root, "Notes.Meta.Draft")
        : SelectedNote is null ? T(_root, "Notes.Meta.Select") : T(_root, "Notes.Meta.LastEdited", FormatTimestamp(SelectedNote.UpdatedAtUtc));

    public void Activate() => _ = ActivateAsync();

    public async Task ActivateAsync()
    {
        _active = true;
        if (!_loaded)
            await LoadAsync();
        else if (Document.IsDirty)
            ScheduleAutoSave();
    }

    public void Deactivate()
    {
        _active = false;
        _autoSave.Cancel();
        AutoSaveStatus = "";
    }

    public void ClearSensitive()
    {
        Deactivate();
        _loaded = false;
        SelectedNote = null;
        List.SearchText = "";
        List.Replace([]);
        Document.Clear();
        Error = "";
        Warning = "";
    }

    private async Task LoadAsync()
    {
        Error = "";
        Warning = "";
        if (_root.VaultPath is not { } path)
        {
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        IsBusy = true;
        var result = await _noteService.LoadAsync(path, _root.VaultKey);
        IsBusy = false;
        if (!result.Success)
        {
            Error = FailureMessage(result.FailureKind);
            return;
        }

        List.Replace(result.Entries.Select(CreateViewModel));
        _skippedCorruptEntries = result.SkippedCorruptEntries;
        Warning = _skippedCorruptEntries > 0
            ? T(_root, "Notes.Warning.CorruptRows", result.SkippedCorruptEntries)
            : "";
        _loaded = true;
        if (SelectedNote is null && !Document.IsCreating)
            ApplySelection(List.MostRecentlyUpdated());
    }

    [RelayCommand]
    private void ToggleNotePicker() => IsNotePickerOpen = !IsNotePickerOpen;

    [RelayCommand]
    private void CloseNotePicker() => IsNotePickerOpen = false;

    [RelayCommand]
    private async Task SelectNoteFromPickerAsync(NoteItemVm? note)
    {
        if (note is null || ReferenceEquals(note, SelectedNote))
            return;
        if (!await CanLeaveDraftAsync())
            return;
        ApplySelection(note);
        IsNotePickerOpen = false;
    }

    [RelayCommand]
    private async Task NewNoteAsync()
    {
        if (!await CanLeaveDraftAsync())
            return;
        _autoSave.Cancel();
        SelectedNote = null;
        Document.New();
        IsNotePickerOpen = false;
        Error = "";
        NotifyDocumentState();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _autoSave.Cancel();
        await SaveCoreAsync(keepEditing: false, logActivity: true, isAutoSave: false, Document.Revision);
    }

    [RelayCommand]
    private void CancelEditing()
    {
        _autoSave.Cancel();
        if (Document.IsCreating)
        {
            Document.Clear();
            ApplySelection(List.MostRecentlyUpdated());
        }
        else
        {
            Document.DiscardChanges();
        }
        AutoSaveStatus = "";
    }

    [RelayCommand]
    private async Task DeleteAsync(NoteItemVm? note = null)
    {
        note ??= SelectedNote;
        if (note is null || _root.VaultPath is not { } path)
        {
            Error = T(_root, "Notes.Error.SelectNoteFirst");
            return;
        }

        var activeDirty = note.Id == SelectedNote?.Id && Document.IsDirty;
        var messageKey = activeDirty ? "Notes.Delete.DirtyMessage" : "Notes.Delete.Message";
        if (!await _root.ConfirmAsync(T(_root, "Notes.Delete.Title"), T(_root, messageKey), T(_root, "Common.Delete"), true))
            return;

        _autoSave.Cancel();
        IsBusy = true;
        var result = await _noteService.DeleteAsync(path, note.Id);
        IsBusy = false;
        if (!result.Success)
        {
            Error = FailureMessage(result.FailureKind);
            return;
        }

        List.Notes.Remove(note);
        List.RefreshPicker();
        if (note.Id == SelectedNote?.Id)
        {
            SelectedNote = null;
            Document.Clear();
            ApplySelection(List.MostRecentlyUpdated());
        }
        _root.LogActivity("notes", "Markdown note deleted", "A markdown note was deleted.", "warning", affectedItem: note.Title);
        await _refreshAllItemsAsync(null);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedNote is null || _root.VaultPath is null)
            return;
        if (Document.IsDirty && !await SaveCoreAsync(true, false, false, Document.Revision))
            return;

        var note = SelectedNote;
        var previous = note.IsFavorite;
        var result = await _noteService.UpdateAsync(_root.VaultPath, _root.VaultKey, note.Id, note.CreatedAtUtc,
            new NoteInput(note.Title, note.Content, !previous));
        if (!result.Success || result.Entry is null)
        {
            Error = FailureMessage(result.FailureKind);
            return;
        }

        note.Apply(result.Entry);
        List.RefreshPicker();
        _root.LogActivity("notes", previous ? "Markdown note unstarred" : "Markdown note starred",
            previous ? "A markdown note was unstarred." : "A markdown note was starred.", "info", affectedItem: note.Title);
    }

    [RelayCommand]
    private async Task CopyContentAsync()
    {
        if (string.IsNullOrWhiteSpace(Document.Content))
        {
            Error = T(_root, "Notes.Error.NothingToCopy");
            return;
        }
        try
        {
            await _root.CopyToClipboardAsync(Document.Content);
            _root.LogActivity("notes", "Markdown note copied", "Markdown note content was copied.", affectedItem: SelectedNote?.Title);
        }
        catch
        {
            Error = T(_root, "Notes.Error.CopyFailed");
        }
    }

    [RelayCommand] private void ShowSplitMode() => SetViewMode("split");
    [RelayCommand] private void ShowEditorMode() => SetViewMode("editor");
    [RelayCommand] private void ShowPreviewMode() => SetViewMode("preview");
    [RelayCommand] private void ToggleDocumentView() => SetViewMode(IsPreviewOnlyMode ? "editor" : "preview");

    private void SetViewMode(string mode)
    {
        if (!Document.HasDocument)
            return;
        Document.ViewMode = mode;
        if (mode == "editor")
            Document.IsEditing = true;
        NotifyDocumentState();
    }

    private async Task<bool> CanLeaveDraftAsync()
    {
        if (!Document.IsDirty)
            return true;

        var choice = await _root.ResolveUnsavedChangesAsync(
            T(_root, "Notes.Unsaved.Title"), T(_root, "Notes.Unsaved.Message"),
            T(_root, "Common.Save"), T(_root, "Notes.Unsaved.Discard"));
        if (choice == UnsavedChangesChoice.Cancel)
            return false;
        if (choice == UnsavedChangesChoice.Save)
            return await SaveCoreAsync(false, true, false, Document.Revision);

        Document.DiscardChanges();
        return true;
    }

    private void OnDraftChanged()
    {
        Error = "";
        NotifyDocumentState();
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        if (!_active || !Document.IsDirty || string.IsNullOrWhiteSpace(Document.Title))
            return;
        AutoSaveStatus = T(_root, "Notes.AutoSave.Pending");
        _autoSave.Schedule(Document.Revision, async (revision, ct) =>
            await SaveCoreAsync(true, false, true, revision, ct));
    }

    private async Task<bool> SaveCoreAsync(bool keepEditing, bool logActivity, bool isAutoSave, long revision, CancellationToken ct = default)
    {
        if (_root.VaultPath is not { } path)
        {
            if (!isAutoSave) Error = T(_root, "Common.NoVaultSelected");
            return false;
        }
        if (string.IsNullOrWhiteSpace(Document.Title))
        {
            if (isAutoSave) AutoSaveStatus = T(_root, "Notes.AutoSave.TitleRequired");
            else Error = T(_root, "Validation.TitleRequired");
            return false;
        }

        var sourceId = Document.SourceId;
        var selected = SelectedNote;
        var input = new NoteInput(Document.Title, Document.Content, selected?.IsFavorite ?? false);
        await _saveGate.WaitAsync(ct);
        try
        {
            if (isAutoSave && (!_active || revision != Document.Revision))
                return false;
            IsBusy = true;
            if (isAutoSave) AutoSaveStatus = T(_root, "Notes.AutoSave.Saving");

            var result = sourceId is null
                ? await _noteService.AddAsync(path, _root.VaultKey, input, ct)
                : await _noteService.UpdateAsync(path, _root.VaultKey, sourceId, selected!.CreatedAtUtc, input, ct);

            if (!result.Success || result.Entry is null)
            {
                if (isAutoSave) AutoSaveStatus = T(_root, "Notes.AutoSave.Failed");
                else Error = FailureMessage(result.FailureKind);
                return false;
            }

            if (revision != Document.Revision || sourceId != Document.SourceId)
                return true;

            var note = selected;
            if (note is null)
            {
                note = CreateViewModel(result.Entry);
                List.Notes.Add(note);
                SelectedNote = note;
            }
            else
            {
                note.Apply(result.Entry);
            }
            Document.AcceptPersisted(note, keepEditing);
            List.RefreshPicker();
            AutoSaveStatus = isAutoSave
                ? T(_root, "Notes.AutoSave.SavedAt", _timeProvider.GetLocalNow().ToString("HH:mm:ss"))
                : "";
            await _refreshAllItemsAsync(note.Id);
            if (logActivity)
                _root.LogActivity("notes", sourceId is null ? "Markdown note created" : "Markdown note updated",
                    sourceId is null ? "A markdown note was created." : "A markdown note was updated.",
                    sourceId is null ? "success" : "info", affectedItem: note.Title);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            IsBusy = false;
            _saveGate.Release();
            NotifyDocumentState();
        }
    }

    private void ApplySelection(NoteItemVm? note)
    {
        foreach (var item in List.Notes)
            item.IsSelected = ReferenceEquals(item, note);
        SelectedNote = note;
        if (note is null) Document.Clear(); else Document.Open(note);
        AutoSaveStatus = "";
        Error = "";
        NotifyDocumentState();
    }

    private NoteItemVm CreateViewModel(NoteEntry entry)
        => new(entry.Id, entry.Title, entry.Content, entry.Favorite, entry.CreatedAtUtc, entry.UpdatedAtUtc,
            note => SelectNoteFromPickerAsync(note), note => DeleteAsync(note));

    private string FailureMessage(NoteFailureKind kind) => kind switch
    {
        NoteFailureKind.ValidationFailed => T(_root, "Validation.TitleRequired"),
        NoteFailureKind.ReadFailed => T(_root, "Notes.Error.LoadFailed"),
        NoteFailureKind.DeleteFailed => T(_root, "Notes.Error.DeleteFailed"),
        NoteFailureKind.Unavailable => T(_root, "Common.NoVaultSelected"),
        _ => T(_root, "Notes.Error.SaveFailed")
    };

    private static string FormatTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var timestamp) ? timestamp.ToLocalTime().ToString("HH:mm | MMM dd") : "";

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnWarningChanged(string value) => OnPropertyChanged(nameof(HasWarning));
    partial void OnIsBusyChanged(bool value) => NotifyDocumentState();

    private void NotifyListState()
    {
        OnPropertyChanged(nameof(NoteCount));
        OnPropertyChanged(nameof(HasNotePickerAll));
        OnPropertyChanged(nameof(NotePickerAll));
    }

    private void NotifyDocumentState()
    {
        NotifyLocalized(nameof(EditorTitle), nameof(EditorContent), nameof(PreviewBlocks), nameof(HasPreviewContent),
            nameof(HasEditor), nameof(IsCreatingNote), nameof(IsEditing), nameof(IsSplitMode), nameof(IsEditorOnlyMode),
            nameof(IsPreviewOnlyMode), nameof(CanSave), nameof(ShowHeaderCommitButtons), nameof(ShowHeaderCreateButton),
            nameof(CanManageSelection), nameof(CanCopyContent), nameof(FavoriteToggleLabel), nameof(SelectedNoteTitleDisplay), nameof(SelectedNoteMeta));
    }

    public override void RefreshLocalization()
    {
        NotifyDocumentState();
        if (_skippedCorruptEntries > 0)
            Warning = T(_root, "Notes.Warning.CorruptRows", _skippedCorruptEntries);
    }
}

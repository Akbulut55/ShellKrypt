using CommunityToolkit.Mvvm.ComponentModel;
using AvaloniaEdit.Document;
using ShellKrypt.Application.Markdown;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public sealed partial class NoteDocumentState : ViewModelBase
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _previewDelay;
    private CancellationTokenSource? _previewCancellation;
    private Task _previewUpdate = Task.CompletedTask;
    private int _previewGeneration;
    private int _contentGeneration;
    private bool _previewStale = true;
    private bool _suppressChanges;
    private bool _active = true;
    private bool _isDirty;

    [ObservableProperty] private IReadOnlyList<MarkdownBlock> previewBlocks = Array.Empty<MarkdownBlock>();
    [ObservableProperty] private string title = "";
    [ObservableProperty] private bool isCreating;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string viewMode = "preview";

    public NoteDocumentState(TimeProvider? timeProvider = null, TimeSpan? previewDelay = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _previewDelay = previewDelay ?? TimeSpan.FromMilliseconds(200);
        EditorDocument.TextChanged += (_, _) => OnEditorDocumentChanged();
    }

    public event EventHandler? DraftChanged;
    public TextDocument EditorDocument { get; } = new();
    public bool HasDocument => IsCreating || !string.IsNullOrWhiteSpace(SourceId);
    public string? SourceId { get; private set; }
    public string OriginalTitle { get; private set; } = "";
    public string OriginalContent { get; private set; } = "";
    public string Content
    {
        get => EditorDocument.Text;
        set
        {
            value ??= string.Empty;
            if (EditorDocument.TextLength == value.Length &&
                string.Equals(EditorDocument.Text, value, StringComparison.Ordinal))
                return;
            EditorDocument.Text = value;
        }
    }
    public bool IsDirty => _isDirty;
    public long Revision { get; private set; }
    public bool HasPreview => PreviewBlocks.Count > 0;

    partial void OnTitleChanged(string value) => Changed();

    private void OnEditorDocumentChanged()
    {
        Interlocked.Increment(ref _contentGeneration);
        OnPropertyChanged(nameof(Content));
        _previewStale = true;
        if (!_suppressChanges && RequiresPreview(ViewMode))
            RequestPreview(immediate: false);
        Changed();
    }

    partial void OnViewModeChanged(string value)
    {
        if (_suppressChanges)
            return;
        if (RequiresPreview(value))
            RequestPreview(immediate: true);
        else
            CancelPreview();
    }

    partial void OnPreviewBlocksChanged(IReadOnlyList<MarkdownBlock> value)
        => OnPropertyChanged(nameof(HasPreview));

    public void Activate()
    {
        _active = true;
        if (_previewStale && RequiresPreview(ViewMode))
            RequestPreview(immediate: true);
    }

    public void Deactivate()
    {
        _active = false;
        CancelPreview();
    }

    public Task AwaitPreviewAsync() => _previewUpdate;

    public void Open(NoteItemVm note)
        => SetDocument(note.Id, note.Title, note.Content, creating: false, editing: false, "preview", resetUndoHistory: true);

    public void New()
        => SetDocument(null, "", "", creating: true, editing: true, "editor", resetUndoHistory: true);

    public void AcceptPersisted(NoteItemVm note, bool keepEditing)
        => SetDocument(note.Id, note.Title, note.Content, creating: false, editing: keepEditing,
            keepEditing ? ViewMode : "preview", resetUndoHistory: !keepEditing);

    public void DiscardChanges()
    {
        _suppressChanges = true;
        Title = OriginalTitle;
        Content = OriginalContent;
        IsEditing = false;
        ViewMode = "preview";
        Revision++;
        _suppressChanges = false;
        EditorDocument.UndoStack.ClearAll();
        SetDirty(false);
        _previewStale = true;
        RequestPreview(immediate: true);
        NotifyState();
    }

    public void Clear()
    {
        CancelPreview();
        _suppressChanges = true;
        SourceId = null;
        OriginalTitle = "";
        OriginalContent = "";
        Title = "";
        Content = "";
        IsCreating = false;
        IsEditing = false;
        ViewMode = "preview";
        Revision++;
        _suppressChanges = false;
        EditorDocument.UndoStack.ClearAll();
        SetDirty(false);
        PreviewBlocks = Array.Empty<MarkdownBlock>();
        _previewStale = false;
        NotifyState();
    }

    private void SetDocument(
        string? id,
        string title,
        string content,
        bool creating,
        bool editing,
        string viewMode,
        bool resetUndoHistory)
    {
        CancelPreview();
        var canReusePreview = !_previewStale && string.Equals(Content, content, StringComparison.Ordinal);
        _suppressChanges = true;
        SourceId = id;
        OriginalTitle = title;
        OriginalContent = content;
        Title = title;
        Content = content;
        IsCreating = creating;
        IsEditing = editing;
        ViewMode = viewMode;
        Revision++;
        _suppressChanges = false;
        if (resetUndoHistory)
            EditorDocument.UndoStack.ClearAll();
        SetDirty(false);
        _previewStale = !canReusePreview;
        if (_previewStale && RequiresPreview(viewMode))
            RequestPreview(immediate: true);
        NotifyState();
    }

    private void Changed()
    {
        if (_suppressChanges)
            return;
        IsEditing = true;
        Revision++;
        UpdateDirty();
        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RequestPreview(bool immediate)
    {
        CancelPreview();
        if (!_active || !_previewStale || !RequiresPreview(ViewMode))
            return;

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        var generation = ++_previewGeneration;
        var contentGeneration = Volatile.Read(ref _contentGeneration);
        var sourceSnapshot = EditorDocument.CreateSnapshot();
        if (immediate && PreviewBlocks.Count > 0)
            PreviewBlocks = Array.Empty<MarkdownBlock>();
        _previewUpdate = BuildPreviewAsync(sourceSnapshot, contentGeneration, generation, immediate, cancellation.Token);
    }

    private async Task BuildPreviewAsync(
        ITextSource sourceSnapshot,
        int contentGeneration,
        int generation,
        bool immediate,
        CancellationToken ct)
    {
        try
        {
            if (!immediate && _previewDelay > TimeSpan.Zero)
                await Task.Delay(_previewDelay, _timeProvider, ct);

            var snapshot = sourceSnapshot.Text;
            var blocks = await Task.Run(() => SimpleMarkdown.Parse(snapshot), ct);
            if (ct.IsCancellationRequested || generation != _previewGeneration ||
                contentGeneration != Volatile.Read(ref _contentGeneration))
                return;

            PreviewBlocks = blocks;
            _previewStale = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void CancelPreview()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        _previewGeneration++;
    }

    private static bool RequiresPreview(string viewMode)
        => viewMode is "preview" or "split";

    private void UpdateDirty()
    {
        if (!HasDocument)
        {
            SetDirty(false);
            return;
        }

        var normalizedTitle = Title.Trim();
        var titleChanged = normalizedTitle.Length != OriginalTitle.Length ||
            !string.Equals(normalizedTitle, OriginalTitle, StringComparison.Ordinal);
        var contentChanged = EditorDocument.TextLength != OriginalContent.Length ||
            !string.Equals(EditorDocument.Text, OriginalContent, StringComparison.Ordinal);
        SetDirty(titleChanged || contentChanged);
    }

    private void SetDirty(bool value)
    {
        if (_isDirty == value)
            return;
        _isDirty = value;
        OnPropertyChanged(nameof(IsDirty));
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(SourceId));
    }
}

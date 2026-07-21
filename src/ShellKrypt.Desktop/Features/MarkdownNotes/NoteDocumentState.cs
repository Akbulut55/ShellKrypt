using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Markdown;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public sealed partial class NoteDocumentState : ViewModelBase
{
    private bool _suppressChanges;

    public ObservableCollection<MarkdownBlock> PreviewBlocks { get; } = new();

    [ObservableProperty] private string title = "";
    [ObservableProperty] private string content = "";
    [ObservableProperty] private bool isCreating;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string viewMode = "preview";
    [ObservableProperty] private long revision;

    public event EventHandler? DraftChanged;
    public bool HasDocument => IsCreating || !string.IsNullOrWhiteSpace(SourceId);
    public string? SourceId { get; private set; }
    public string OriginalTitle { get; private set; } = "";
    public string OriginalContent { get; private set; } = "";
    public bool IsDirty => HasDocument &&
        (!string.Equals(Title.Trim(), OriginalTitle, StringComparison.Ordinal) ||
         !string.Equals(Content, OriginalContent, StringComparison.Ordinal));

    partial void OnTitleChanged(string value) => Changed();
    partial void OnContentChanged(string value)
    {
        RebuildPreview();
        Changed();
    }

    public void Open(NoteItemVm note)
    {
        SetDocument(note.Id, note.Title, note.Content, creating: false, editing: false, "preview");
    }

    public void New()
    {
        SetDocument(null, "", "", creating: true, editing: true, "editor");
    }

    public void AcceptPersisted(NoteItemVm note, bool keepEditing)
    {
        SetDocument(note.Id, note.Title, note.Content, creating: false, editing: keepEditing,
            keepEditing ? ViewMode : "preview");
    }

    public void DiscardChanges()
    {
        _suppressChanges = true;
        Title = OriginalTitle;
        Content = OriginalContent;
        _suppressChanges = false;
        IsEditing = false;
        ViewMode = "preview";
        Revision++;
        NotifyState();
    }

    public void Clear()
    {
        SetDocument(null, "", "", creating: false, editing: false, "preview");
        PreviewBlocks.Clear();
    }

    private void SetDocument(string? id, string title, string content, bool creating, bool editing, string viewMode)
    {
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
        RebuildPreview();
        NotifyState();
    }

    private void Changed()
    {
        if (_suppressChanges)
            return;
        IsEditing = true;
        Revision++;
        NotifyState();
        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildPreview()
    {
        PreviewBlocks.Clear();
        foreach (var block in SimpleMarkdown.Parse(Content))
            PreviewBlocks.Add(block);
        OnPropertyChanged(nameof(HasPreview));
    }

    public bool HasPreview => PreviewBlocks.Count > 0;

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(SourceId));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Notes;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public sealed partial class NoteItemVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string content;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private bool isSelected;

    public IAsyncRelayCommand SelectCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    public NoteItemVm(
        string id,
        string title,
        string content,
        bool favorite,
        string createdAtUtc,
        string updatedAtUtc,
        Func<NoteItemVm, Task>? select = null,
        Func<NoteItemVm, Task>? delete = null)
    {
        Id = id;
        Title = title;
        Content = content;
        IsFavorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        SelectCommand = new AsyncRelayCommand(() => select?.Invoke(this) ?? Task.CompletedTask);
        DeleteCommand = new AsyncRelayCommand(() => delete?.Invoke(this) ?? Task.CompletedTask);
    }

    public void Apply(NoteEntry entry)
    {
        Title = entry.Title;
        Content = entry.Content;
        IsFavorite = entry.Favorite;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        OnPropertyChanged(nameof(UpdatedAtUtc));
    }
}

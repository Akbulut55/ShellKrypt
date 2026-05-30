using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Core.Items;
using System;

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

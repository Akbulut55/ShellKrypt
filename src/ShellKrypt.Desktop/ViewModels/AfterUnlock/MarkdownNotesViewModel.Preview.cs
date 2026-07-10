using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Markdown;
using System;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MarkdownNotesViewModel
{
    [RelayCommand]
    private void ToggleDocumentView()
    {
        if (!HasEditor)
            return;

        ActiveDocumentView = IsEditorOnlyMode ? "preview" : "editor";
    }

    [RelayCommand]
    private void CycleDocumentView()
    {
        if (!HasEditor)
            return;

        ActiveDocumentView = ActiveDocumentView switch
        {
            "editor" => "preview",
            "preview" => "split",
            _ => "editor"
        };
    }

    [RelayCommand]
    private void ShowSplitMode()
    {
        ActiveDocumentView = "split";
    }

    [RelayCommand]
    private void ShowEditorMode()
    {
        if (!IsEditing && HasEditor)
            IsEditing = true;

        ActiveDocumentView = "editor";
    }

    [RelayCommand]
    private void ShowPreviewMode()
    {
        ActiveDocumentView = "preview";
    }

    private void RefreshPreviewContent()
    {
        PreviewBlocks.Clear();
        foreach (var block in SimpleMarkdown.Parse(EditorContent))
            PreviewBlocks.Add(block);
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
}

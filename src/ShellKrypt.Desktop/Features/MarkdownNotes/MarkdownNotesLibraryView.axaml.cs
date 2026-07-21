using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesLibraryView : UserControl
{
    public MarkdownNotesLibraryView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (Design.IsDesignMode && DataContext is null)
            DataContext = MarkdownNotesDesignData.CreateLibraryOpen();
    }
}

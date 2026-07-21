using Avalonia.Controls;
using Avalonia;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNoteEditorView : UserControl
{
    public MarkdownNoteEditorView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Design.IsDesignMode && DataContext is null)
            DataContext = MarkdownNotesDesignData.CreateEditing();
    }
}

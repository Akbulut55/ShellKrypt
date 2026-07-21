using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public partial class MarkdownNotesView : UserControl
{
    public MarkdownNotesView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
            DataContext = MarkdownNotesDesignData.CreateSplit();
    }

}

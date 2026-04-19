using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Views;

public partial class MarkdownNotesView : UserControl
{
    public MarkdownNotesView()
    {
        InitializeComponent();
    }

    private void OnNoteItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: NoteItemVm note })
            return;

        if (DataContext is MarkdownNotesViewModel viewModel)
            viewModel.SelectNote(note);
    }
}

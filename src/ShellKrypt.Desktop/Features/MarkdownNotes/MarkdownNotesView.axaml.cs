using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShellKrypt.Desktop.Features.MarkdownNotes;

namespace ShellKrypt.Desktop.Features.MarkdownNotes;

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

    private void OnNotePickerItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: NoteItemVm note })
            return;

        if (DataContext is MarkdownNotesViewModel viewModel)
        {
            viewModel.SelectNote(note);
            NotePickerButton.Flyout?.Hide();
        }
    }

    private void OnNotePickerItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: NoteItemVm note })
            return;

        if (DataContext is MarkdownNotesViewModel viewModel)
        {
            viewModel.SelectNote(note);
            NotePickerButton.Flyout?.Hide();
            e.Handled = true;
        }
    }

    private void OnNotePickerDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: NoteItemVm note })
            return;

        if (DataContext is MarkdownNotesViewModel viewModel)
        {
            viewModel.SelectNote(note);
            if (viewModel.DeleteCommand.CanExecute(null))
                viewModel.DeleteCommand.Execute(null);
            NotePickerButton.Flyout?.Hide();
            e.Handled = true;
        }
    }

    private void OnNotePickerAddClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NotePickerButton.Flyout?.Hide();
    }
}

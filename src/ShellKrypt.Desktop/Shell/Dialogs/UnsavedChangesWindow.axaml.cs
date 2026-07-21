using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Shell.Dialogs;

public partial class UnsavedChangesWindow : Window
{
    public UnsavedChangesWindow() => InitializeComponent();

    public UnsavedChangesWindow(string title, string message, string saveText, string discardText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        SaveButton.Content = saveText;
        DiscardButton.Content = discardText;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);
    private void OnDiscardClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}

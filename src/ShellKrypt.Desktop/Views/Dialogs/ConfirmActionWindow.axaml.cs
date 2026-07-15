using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views;

public partial class ConfirmActionWindow : Window
{
    public ConfirmActionWindow()
    {
        InitializeComponent();
        DetailPanel.IsVisible = false;
    }

    public ConfirmActionWindow(string title, string message, string detail, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        DetailText.Text = detail;
        DetailPanel.IsVisible = !string.IsNullOrWhiteSpace(detail);
        ConfirmButton.Content = confirmText;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(true);
}

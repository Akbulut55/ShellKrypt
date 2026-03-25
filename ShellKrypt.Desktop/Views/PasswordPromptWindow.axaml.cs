using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views;

public partial class PasswordPromptWindow : Window
{
    public PasswordPromptWindow()
    {
        InitializeComponent();
        Opened += (_, _) => PasswordBox.Focus();
    }

    public PasswordPromptWindow(string title, string message, string detail, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        DetailText.Text = detail;
        ConfirmButton.Content = confirmText;
        Opened += (_, _) => PasswordBox.Focus();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(PasswordBox.Text);
}

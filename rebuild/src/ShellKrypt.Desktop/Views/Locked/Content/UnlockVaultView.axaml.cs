using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views.Locked.Content;

public partial class UnlockVaultView : UserControl
{
    private bool _isPasswordVisible;

    public event EventHandler? BackRequested;

    public UnlockVaultView()
        => InitializeComponent();

    private void OnBackClicked(object? sender, RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnTogglePasswordVisibilityClicked(object? sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordInput.PasswordChar = _isPasswordVisible ? '\0' : '⦁';
    }

    private void OnPasswordInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            UnlockButton.Classes.Add("keyboard-pressed");
    }

    private void OnPasswordInputKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            UnlockButton.Classes.Remove("keyboard-pressed");
    }
}

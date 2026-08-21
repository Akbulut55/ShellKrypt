using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views.Locked.Content;

public partial class UnlockVaultView : UserControl
{
    public event EventHandler? BackRequested;

    public UnlockVaultView()
        => InitializeComponent();

    private void OnBackClicked(object? sender, RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}

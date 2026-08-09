using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views.Locked.Content;

public partial class VaultListView : UserControl
{
    public event EventHandler? VaultSelected;

    public VaultListView()
        => InitializeComponent();

    private void OnSelectVaultClick(object? sender, RoutedEventArgs e)
        => VaultSelected?.Invoke(this, EventArgs.Empty);
}

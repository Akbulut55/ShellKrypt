using Avalonia.Controls;

namespace ShellKrypt.Desktop.Views.Locked.Content;

public partial class VaultListView : UserControl
{
    public event EventHandler? VaultSelected;

    public IReadOnlyList<VaultListItem> Vaults { get; } =
    [
        new("Personal Vault", "Last opened: Today at 09:42"),
        new("Work Vault", "Last opened: Yesterday at 17:30"),
        new("Archive Vault", "Last opened: 3 days ago"),
        new("Development Vault", "Last opened: Last week"),
        new("Travel Vault", "Last opened: 2 weeks ago")
    ];

    public VaultListView()
        => InitializeComponent();

    private void OnVaultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: not null })
            VaultSelected?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record VaultListItem(string Name, string LastOpened);

using Avalonia.Controls;
using ShellKrypt.Desktop.Views.Locked.Content;

namespace ShellKrypt.Desktop.Views.Locked;

public partial class LockedView : UserControl
{
    public LockedView()
        => InitializeComponent();

    private async void OnVaultSelected(object? sender, EventArgs e)
    {
        RightContent.Content = new UnlockVaultView();

        await Task.Delay(150);

        LeftContent.Content = new VaultDetailsView();
    }
}

using Avalonia.Controls;
using ShellKrypt.Desktop.Views.Locked.Content;

namespace ShellKrypt.Desktop.Views.Locked;

public partial class LockedView : UserControl
{
    public LockedView()
        => InitializeComponent();

    private async void OnVaultSelected(object? sender, EventArgs e)
    {
        LeftContent.IsTransitionReversed = false;
        RightContent.IsTransitionReversed = false;

        var unlockVaultView = new UnlockVaultView();
        unlockVaultView.BackRequested += OnBackRequested;
        RightContent.Content = unlockVaultView;

        await Task.Delay(300);

        LeftContent.Content = new VaultDetailsView();
    }

    private async void OnBackRequested(object? sender, EventArgs e)
    {
        LeftContent.IsTransitionReversed = true;
        RightContent.IsTransitionReversed = true;

        var vaultListView = new VaultListView();
        vaultListView.VaultSelected += OnVaultSelected;
        RightContent.Content = vaultListView;

        await Task.Delay(300);

        LeftContent.Content = new WelcomeView();
    }
}

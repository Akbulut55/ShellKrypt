using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Shell.Dialogs;

public partial class EditVaultWindow : Window
{
    public EditVaultWindow()
    {
        InitializeComponent();
    }

    public EditVaultWindow(string displayName, string description, string vaultPath)
        : this()
    {
        DisplayNameBox.Text = displayName;
        DescriptionBox.Text = description;
        PathText.Text = vaultPath;
    }

    public string DisplayName => DisplayNameBox.Text?.Trim() ?? "";
    public string Description => DescriptionBox.Text ?? "";

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Close(true);
}

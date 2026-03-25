using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ShellKrypt.Desktop.Views;

public partial class ImportVaultWindow : Window
{
    public ImportVaultWindow()
    {
        InitializeComponent();
    }

    public ImportVaultWindow(string? initialPath, string? initialDisplayName)
        : this()
    {
        PathBox.Text = initialPath ?? "";
        DisplayNameBox.Text = initialDisplayName ?? "";
    }

    public string VaultPath => PathBox.Text?.Trim() ?? "";
    public string DisplayName => DisplayNameBox.Text?.Trim() ?? "";

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select existing vault",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ShellKrypt Vault")
                {
                    Patterns = ["*.skvault"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        PathBox.Text = path;
        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
            DisplayNameBox.Text = Path.GetFileNameWithoutExtension(path);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        ErrorText.Text = "";

        if (string.IsNullOrWhiteSpace(VaultPath))
        {
            ErrorText.Text = "Choose a vault file first.";
            ErrorText.IsVisible = true;
            return;
        }

        Close(true);
    }
}

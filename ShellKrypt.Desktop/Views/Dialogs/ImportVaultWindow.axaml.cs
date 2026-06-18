using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
            Title = Loc("Dialog.ImportVault.PickerTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc("Dialog.ImportVault.FileType"))
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

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        ErrorText.Text = "";

        if (string.IsNullOrWhiteSpace(VaultPath))
        {
            ErrorText.Text = Loc("Dialog.ImportVault.ErrorChooseFile");
            ErrorText.IsVisible = true;
            return;
        }

        Close(true);
    }

    private static string Loc(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource($"Loc.{key}", null, out var value) == true &&
            value is string text)
        {
            return text;
        }

        return key;
    }
}

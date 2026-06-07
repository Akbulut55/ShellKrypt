using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task BrowseEncryptedExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            "Choose encrypted backup location",
            Path.GetFileNameWithoutExtension(EncryptedExportPath),
            ".skbx",
            [".skbx"],
            "ShellKrypt Backup");

        if (!string.IsNullOrWhiteSpace(path))
            EncryptedExportPath = path;
    }

    [RelayCommand]
    private async Task BrowsePlaintextExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            "Choose plaintext export location",
            Path.GetFileNameWithoutExtension(PlaintextExportPath),
            ".json",
            [".json"],
            "JSON Export");

        if (!string.IsNullOrWhiteSpace(path))
            PlaintextExportPath = path;
    }

    [RelayCommand]
    private async Task BrowseEncryptedImportPathAsync()
    {
        var path = await _root.PickOpenFileAsync(
            "Select encrypted backup",
            [".skbx"],
            "ShellKrypt Backup");

        if (!string.IsNullOrWhiteSpace(path))
            EncryptedImportPath = path;
    }

    [RelayCommand]
    private async Task BrowseCsvImportPathAsync()
    {
        var path = await _root.PickOpenFileAsync(
            "Select CSV import file",
            [".csv"],
            "CSV File");

        if (!string.IsNullOrWhiteSpace(path))
            CsvImportPath = path;
    }
}

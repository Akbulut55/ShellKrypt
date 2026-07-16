using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ShellKrypt.Desktop.Shell.Dialogs;

namespace ShellKrypt.Desktop.Shell.Dialogs;

public sealed class DesktopDialogService(SessionSecurityService sessionSecurity) : IDesktopDialogService
{
    public async Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(fileTypeName) { Patterns = extensions.Select(ToPattern).ToArray() }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedName) ? "file" : suggestedName,
            DefaultExtension = defaultExtension.TrimStart('.'),
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(fileTypeName) { Patterns = extensions.Select(ToPattern).ToArray() }]
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.StorageProvider: { } storageProvider })
            return null;

        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText)
    {
        if (GetMainWindow() is not { } mainWindow)
            return false;
        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        return await new ConfirmActionWindow(title, message, detail, confirmText).ShowDialog<bool>(mainWindow);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false)
    {
        if (GetMainWindow() is not { } mainWindow)
            return false;
        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        return await new ConfirmActionWindow(title, message, "", confirmText).ShowDialog<bool>(mainWindow);
    }

    public async Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText)
    {
        if (GetMainWindow() is not { } mainWindow)
            return null;
        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        return await new PasswordPromptWindow(title, message, detail, confirmText).ShowDialog<string?>(mainWindow);
    }

    public async Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null)
    {
        if (GetMainWindow() is not { } mainWindow)
            return (false, "", "");
        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new ImportVaultWindow(initialPath, initialDisplayName);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.VaultPath, dialog.DisplayName);
    }

    public async Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath)
    {
        if (GetMainWindow() is not { } mainWindow)
            return (false, displayName, description);
        using var _ = sessionSecurity.SuppressTransientFocusLoss();
        var dialog = new EditVaultWindow(displayName, description, vaultPath);
        var confirmed = await dialog.ShowDialog<bool>(mainWindow);
        return (confirmed, dialog.DisplayName, dialog.Description);
    }

    private static Avalonia.Controls.Window? GetMainWindow()
        => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static string ToPattern(string extension)
    {
        var normalized = extension?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
            return "*.*";
        return normalized.StartsWith(".", StringComparison.Ordinal) ? $"*{normalized}" : $"*.{normalized.TrimStart('*')}";
    }
}

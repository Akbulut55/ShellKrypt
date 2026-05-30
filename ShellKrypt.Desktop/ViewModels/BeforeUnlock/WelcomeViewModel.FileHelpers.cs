using System.IO;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    private static void CopySidecarIfExists(string sourcePath, string targetPath, string suffix)
    {
        var source = sourcePath + suffix;
        if (!File.Exists(source))
            return;

        File.Copy(source, targetPath + suffix, overwrite: false);
    }

    private static void DeleteSidecarIfExists(string vaultPath, string suffix)
    {
        var sidecar = vaultPath + suffix;
        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }

    private static long GetVaultSize(string path)
        => File.Exists(path) ? new FileInfo(path).Length : 0L;

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        decimal display = bytes;
        var unitIndex = 0;

        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return $"{display:0.##} {units[unitIndex]}";
    }

    private enum SecurityAcknowledgementAction
    {
        None,
        CreateVault,
        ImportVault,
        OpenVault
    }
}

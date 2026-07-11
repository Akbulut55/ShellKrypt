using System;
using System.IO;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    private string GetVaultDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Vault";

        return Path.GetFileNameWithoutExtension(_root.VaultPath);
    }

    private string GetVaultStorageDisplay()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return T("Settings.Storage.Used", "640 MB");

        var bytes = new FileInfo(_root.VaultPath).Length;
        return T("Settings.Storage.Used", FormatBytes(bytes));
    }

    private double GetVaultStoragePercent()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return 64;

        const double oneGb = 1024d * 1024d * 1024d;
        var bytes = new FileInfo(_root.VaultPath).Length;
        return Math.Clamp(bytes / oneGb * 100d, 0d, 100d);
    }

    private string GetVaultFileName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Personal_Vault_v2.skryp";

        return Path.GetFileName(_root.VaultPath);
    }

    private static string FormatBytes(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        if (bytes >= gigabyte)
            return $"{bytes / gigabyte:0.#} GB";

        if (bytes >= megabyte)
            return $"{bytes / megabyte:0.#} MB";

        if (bytes >= kilobyte)
            return $"{bytes / kilobyte:0.#} KB";

        return $"{bytes} B";
    }
}

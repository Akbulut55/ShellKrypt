using System;
using System.Collections.Generic;
using System.IO;

namespace ShellKrypt.Desktop.Services;

public static class DefaultPaths
{
    public static string AppRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShellKrypt");

    public static string VaultsRoot => Path.Combine(AppRoot, "Vaults");

    public static string DefaultVaultPath => Path.Combine(VaultsRoot, "ShellKrypt.skvault");

    public static string VaultRegistryPath => Path.Combine(AppRoot, "vaults.json");

    public static string SettingsPath => Path.Combine(AppRoot, "settings.json");

    public static string GetSuggestedVaultPath(string? displayName)
    {
        Directory.CreateDirectory(VaultsRoot);

        var baseName = NormalizeFileName(string.IsNullOrWhiteSpace(displayName) ? "Vault" : displayName);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Vault";

        var candidate = Path.Combine(VaultsRoot, $"{baseName}.skvault");
        if (!File.Exists(candidate))
            return candidate;

        for (var i = 2; i < 1000; i++)
        {
            candidate = Path.Combine(VaultsRoot, $"{baseName} ({i}).skvault");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(VaultsRoot, $"{baseName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.skvault");
    }

    private static string NormalizeFileName(string value)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var ch in value.Trim())
        {
            buffer[index++] = invalid.Contains(ch) ? ' ' : ch;
        }

        return new string(buffer[..index]).Trim();
    }
}

using System.Text.Json;
using ShellKrypt.Application.Ports;
using ShellKrypt.Application.Vaulting;

namespace ShellKrypt.Infrastructure.Services;

public sealed class FileVaultRegistryStore : IVaultRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public VaultRegistry Load()
    {
        try
        {
            var registry = File.Exists(DefaultPaths.VaultRegistryPath)
                ? JsonSerializer.Deserialize<VaultRegistry>(File.ReadAllText(DefaultPaths.VaultRegistryPath), JsonOptions) ?? new VaultRegistry()
                : new VaultRegistry();

            MigrateLegacyVaultIfNeeded(registry);
            return registry;
        }
        catch
        {
            var fallback = new VaultRegistry();
            MigrateLegacyVaultIfNeeded(fallback);
            return fallback;
        }
    }

    public void Save(VaultRegistry registry)
    {
        var dir = Path.GetDirectoryName(DefaultPaths.VaultRegistryPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(registry, JsonOptions);
        File.WriteAllText(DefaultPaths.VaultRegistryPath, json);
    }

    private static void MigrateLegacyVaultIfNeeded(VaultRegistry registry)
    {
        var legacyPath = NormalizePath(DefaultPaths.DefaultVaultPath);
        if (!File.Exists(legacyPath))
            return;

        if (registry.Vaults.Any(x => string.Equals(NormalizePath(x.VaultPath), legacyPath, StringComparison.OrdinalIgnoreCase)))
            return;

        registry.Vaults.Add(new VaultRegistryEntry
        {
            VaultPath = legacyPath,
            DisplayName = Path.GetFileNameWithoutExtension(legacyPath),
            Description = "",
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            LastOpenedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            IsDefault = true
        });
    }

    private static string NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);
}

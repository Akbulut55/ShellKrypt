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

            return registry;
        }
        catch
        {
            return new VaultRegistry();
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
}

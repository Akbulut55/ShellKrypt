namespace ShellKrypt.Application.Vaulting;

public sealed partial class VaultRegistryService
{
    public VaultRegistryEntry UpsertVault(
        string vaultPath,
        string displayName,
        string description,
        bool isDefault = false,
        bool markOpened = false)
    {
        var registry = Load();
        var path = NormalizePath(vaultPath);
        var entry = registry.Vaults.FirstOrDefault(x =>
            string.Equals(NormalizePath(x.VaultPath), path, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            entry = new VaultRegistryEntry
            {
                VaultPath = path,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            registry.Vaults.Add(entry);
        }

        entry.VaultPath = path;
        entry.DisplayName = NormalizeLabel(displayName, Path.GetFileNameWithoutExtension(path));
        entry.Description = description?.Trim() ?? "";

        if (markOpened)
            entry.LastOpenedAtUtc = DateTimeOffset.UtcNow.ToString("O");

        if (isDefault)
            SetDefaultInternal(registry, path);

        Save(registry);
        return Clone(entry);
    }

    public VaultRegistryEntry MarkOpened(string vaultPath)
    {
        var path = NormalizePath(vaultPath);
        var registry = Load();
        var entry = registry.Vaults.FirstOrDefault(x =>
            string.Equals(NormalizePath(x.VaultPath), path, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            entry = new VaultRegistryEntry
            {
                VaultPath = path,
                DisplayName = Path.GetFileNameWithoutExtension(path),
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            registry.Vaults.Add(entry);
        }

        entry.LastOpenedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        Save(registry);
        return Clone(entry);
    }

    public VaultRegistryEntry? SetDefaultVault(string vaultPath)
    {
        var path = NormalizePath(vaultPath);
        var registry = Load();
        var entry = registry.Vaults.FirstOrDefault(x =>
            string.Equals(NormalizePath(x.VaultPath), path, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return null;

        SetDefaultInternal(registry, path);
        Save(registry);
        return Clone(entry);
    }

    public bool RemoveVault(string vaultPath)
    {
        var path = NormalizePath(vaultPath);
        var registry = Load();
        var removed = registry.Vaults.RemoveAll(x =>
            string.Equals(NormalizePath(x.VaultPath), path, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return false;

        Save(registry);
        return true;
    }
}

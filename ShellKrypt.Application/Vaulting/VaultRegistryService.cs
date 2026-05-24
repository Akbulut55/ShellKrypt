using ShellKrypt.Application.Ports;

namespace ShellKrypt.Application.Vaulting;

public sealed class VaultRegistryService
{
    private readonly IVaultRegistryStore _store;

    public VaultRegistryService(IVaultRegistryStore store)
    {
        _store = store;
    }

    public VaultRegistry Load() => NormalizeRegistry(_store.Load());

    public void Save(VaultRegistry registry) => _store.Save(NormalizeRegistry(registry));

    public IReadOnlyList<VaultRegistryEntry> ListVaults()
        => Load().Vaults
            .OrderByDescending(x => x.LastOpenedAtUtc)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToArray();

    public IReadOnlyList<VaultRegistryEntry> ListRecentVaults(int maxCount = 5)
        => ListVaults()
            .Where(x => !string.IsNullOrWhiteSpace(x.LastOpenedAtUtc))
            .Take(maxCount)
            .ToArray();

    public VaultRegistryEntry? GetDefaultVault()
        => ListVaults().FirstOrDefault(x => x.IsDefault) ?? ListVaults().FirstOrDefault();

    public VaultRegistryEntry? FindByPath(string vaultPath)
    {
        var normalizedPath = NormalizePath(vaultPath);
        return Load().Vaults
            .FirstOrDefault(x => string.Equals(NormalizePath(x.VaultPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
            is { } entry
                ? Clone(entry)
                : null;
    }

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

    private static VaultRegistry NormalizeRegistry(VaultRegistry registry)
    {
        var normalized = new VaultRegistry();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var vault in registry.Vaults)
        {
            var path = NormalizePath(vault.VaultPath);
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                continue;

            normalized.Vaults.Add(new VaultRegistryEntry
            {
                Id = string.IsNullOrWhiteSpace(vault.Id) ? Guid.NewGuid().ToString("N") : vault.Id,
                VaultPath = path,
                DisplayName = NormalizeLabel(vault.DisplayName, Path.GetFileNameWithoutExtension(path)),
                Description = NormalizeDescription(vault.Description),
                CreatedAtUtc = string.IsNullOrWhiteSpace(vault.CreatedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : vault.CreatedAtUtc,
                LastOpenedAtUtc = string.IsNullOrWhiteSpace(vault.LastOpenedAtUtc) ? null : vault.LastOpenedAtUtc,
                IsDefault = vault.IsDefault
            });
        }

        if (normalized.Vaults.Any() && !normalized.Vaults.Any(x => x.IsDefault))
            normalized.Vaults[0].IsDefault = true;

        return normalized;
    }

    private static void SetDefaultInternal(VaultRegistry registry, string vaultPath)
    {
        foreach (var vault in registry.Vaults)
            vault.IsDefault = string.Equals(NormalizePath(vault.VaultPath), vaultPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);

    private static string NormalizeLabel(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string NormalizeDescription(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return string.Equals(trimmed, "Legacy default vault", StringComparison.OrdinalIgnoreCase) ? "" : trimmed;
    }

    private static VaultRegistryEntry Clone(VaultRegistryEntry entry)
        => new()
        {
            Id = entry.Id,
            VaultPath = entry.VaultPath,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            CreatedAtUtc = entry.CreatedAtUtc,
            LastOpenedAtUtc = entry.LastOpenedAtUtc,
            IsDefault = entry.IsDefault
        };
}

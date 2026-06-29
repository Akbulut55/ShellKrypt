namespace ShellKrypt.Application.Vaulting;

public sealed partial class VaultRegistryService
{
    public IReadOnlyList<VaultRegistryEntry> ListVaults()
        => Load().Vaults
            .OrderByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.LastOpenedAtUtc)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToArray();

    public IReadOnlyList<VaultRegistryEntry> ListRecentVaults(int maxCount = 5)
        => ListVaults()
            .Where(x => !string.IsNullOrWhiteSpace(x.LastOpenedAtUtc))
            .Take(maxCount)
            .ToArray();

    public VaultRegistryEntry? FindByPath(string vaultPath)
    {
        var normalizedPath = NormalizePath(vaultPath);
        return Load().Vaults
            .FirstOrDefault(x => string.Equals(NormalizePath(x.VaultPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
            is { } entry
                ? Clone(entry)
                : null;
    }
}

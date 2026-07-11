namespace ShellKrypt.Application.Vaulting;

public sealed partial class VaultRegistryService
{
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
                IsFavorite = vault.IsFavorite
            });
        }

        return normalized;
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
            IsFavorite = entry.IsFavorite
        };
}

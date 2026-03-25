using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShellKrypt.Desktop.Services;

public sealed class VaultRegistryStore
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

        var normalized = NormalizeRegistry(registry);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(DefaultPaths.VaultRegistryPath, json);
    }

    public IReadOnlyList<VaultRegistryEntry> ListVaults()
        => Load().Vaults
            .OrderByDescending(x => x.LastOpenedAtUtc)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
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
        return Load().Vaults.FirstOrDefault(x =>
            string.Equals(NormalizePath(x.VaultPath), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public VaultRegistryEntry UpsertVault(
        string vaultPath,
        string displayName,
        string description,
        string? accentColor = null,
        string? iconKey = null,
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
        entry.AccentColor = string.IsNullOrWhiteSpace(accentColor) ? null : accentColor.Trim();
        entry.IconKey = string.IsNullOrWhiteSpace(iconKey) ? null : iconKey.Trim();

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

    private static void SetDefaultInternal(VaultRegistry registry, string vaultPath)
    {
        foreach (var vault in registry.Vaults)
            vault.IsDefault = string.Equals(NormalizePath(vault.VaultPath), vaultPath, StringComparison.OrdinalIgnoreCase);
    }

    private static VaultRegistry NormalizeRegistry(VaultRegistry registry)
    {
        var normalized = new VaultRegistry();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var vault in registry.Vaults)
        {
            var path = NormalizePath(vault.VaultPath);
            if (!seen.Add(path))
                continue;

            normalized.Vaults.Add(new VaultRegistryEntry
            {
                Id = string.IsNullOrWhiteSpace(vault.Id) ? Guid.NewGuid().ToString("N") : vault.Id,
                VaultPath = path,
                DisplayName = NormalizeLabel(vault.DisplayName, Path.GetFileNameWithoutExtension(path)),
                Description = vault.Description?.Trim() ?? "",
                AccentColor = string.IsNullOrWhiteSpace(vault.AccentColor) ? null : vault.AccentColor.Trim(),
                IconKey = string.IsNullOrWhiteSpace(vault.IconKey) ? null : vault.IconKey.Trim(),
                CreatedAtUtc = string.IsNullOrWhiteSpace(vault.CreatedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : vault.CreatedAtUtc,
                LastOpenedAtUtc = string.IsNullOrWhiteSpace(vault.LastOpenedAtUtc) ? null : vault.LastOpenedAtUtc,
                IsDefault = vault.IsDefault
            });
        }

        if (!normalized.Vaults.Any())
            return normalized;

        if (!normalized.Vaults.Any(x => x.IsDefault))
            normalized.Vaults[0].IsDefault = true;

        return normalized;
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
            Description = "Legacy default vault",
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            LastOpenedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            IsDefault = true
        });
    }

    private static string NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);

    private static string NormalizeLabel(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static VaultRegistryEntry Clone(VaultRegistryEntry entry)
        => new()
        {
            Id = entry.Id,
            VaultPath = entry.VaultPath,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            AccentColor = entry.AccentColor,
            IconKey = entry.IconKey,
            CreatedAtUtc = entry.CreatedAtUtc,
            LastOpenedAtUtc = entry.LastOpenedAtUtc,
            IsDefault = entry.IsDefault
        };
}

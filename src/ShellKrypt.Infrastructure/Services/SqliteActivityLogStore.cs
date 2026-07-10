using System.Text.Json;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Ports;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore : IActivityLogStore
{
    private const int MaxEntries = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<ActivityLogEntry> Load(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(vaultPath) && vaultKey is { Length: > 0 })
            return LoadVaultEntries(vaultPath, vaultKey);

        return [];
    }

    public void Append(ActivityLogEntry entry, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(entry.VaultPath) && vaultKey is { Length: > 0 })
            AppendVaultEntry(entry, vaultKey);

        // Activity logs are vault-scoped and encrypted. Events without a vault key are intentionally not persisted.
    }

    public void Clear(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(vaultPath) && vaultKey is { Length: > 0 })
            ClearVaultEntries(vaultPath);

        // Legacy global activity logs are quarantined by leaving them unread and unwritten.
    }
}

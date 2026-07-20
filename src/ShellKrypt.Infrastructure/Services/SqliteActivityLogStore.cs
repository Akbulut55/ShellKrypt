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

    public ActivityLogLoadResult Load(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(vaultPath) && vaultKey is { Length: > 0 })
            return LoadVaultEntries(vaultPath, vaultKey);

        return new([], 0, ActivityLogFailureKind.Unavailable);
    }

    public ActivityLogOperationResult Append(ActivityLogEntry entry, byte[]? vaultKey = null)
    {
        if (string.IsNullOrWhiteSpace(entry.VaultPath) || vaultKey is not { Length: > 0 })
            return new(ActivityLogFailureKind.Unavailable);

        try
        {
            AppendVaultEntry(entry, vaultKey);
            return ActivityLogOperationResult.Succeeded;
        }
        catch
        {
            return new(ActivityLogFailureKind.WriteFailed);
        }
    }

    public ActivityLogOperationResult Clear(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (string.IsNullOrWhiteSpace(vaultPath) || vaultKey is not { Length: > 0 })
            return new(ActivityLogFailureKind.Unavailable);

        try
        {
            ClearVaultEntries(vaultPath);
            return ActivityLogOperationResult.Succeeded;
        }
        catch
        {
            return new(ActivityLogFailureKind.ClearFailed);
        }
    }
}

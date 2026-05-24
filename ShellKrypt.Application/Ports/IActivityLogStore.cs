using ShellKrypt.Application.Activity;

namespace ShellKrypt.Application.Ports;

public interface IActivityLogStore
{
    IReadOnlyList<ActivityLogEntry> Load(string? vaultPath, byte[]? vaultKey);
    void Append(ActivityLogEntry entry, byte[]? vaultKey);
    void Clear(string? vaultPath, byte[]? vaultKey);
}

using ShellKrypt.Application.Activity;

namespace ShellKrypt.Application.Ports;

public interface IActivityLogStore
{
    ActivityLogLoadResult Load(string? vaultPath, byte[]? vaultKey);
    ActivityLogOperationResult Append(ActivityLogEntry entry, byte[]? vaultKey);
    ActivityLogOperationResult Clear(string? vaultPath, byte[]? vaultKey);
}

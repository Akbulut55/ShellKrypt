namespace ShellKrypt.Infrastructure.Backups.Internal;

internal static class VaultTransferLimits
{
    public const long MaxEncryptedPackageBytes = 64L * 1024 * 1024;
    public const long MaxCsvBytes = 8L * 1024 * 1024;
    public const int MaxSnapshotJsonBytes = 64 * 1024 * 1024;
    public const int MaxSnapshotItems = 10000;
    public const int MaxSnapshotLabels = 2000;
    public const int MaxSnapshotItemLabels = 50000;
    public const int MaxPayloadJsonChars = 1024 * 1024;
    public const int MaxCsvFieldChars = 16384;
}

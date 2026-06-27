namespace ShellKrypt.Infrastructure.ProjectSecrets;

public static class ProjectSecretScannerLimits
{
    public const int MaxFilesScanned = 3000;
    public const int MaxFileSizeBytes = 1024 * 1024;
    public const long MaxTotalBytesScanned = 50L * 1024L * 1024L;
    public const int MaxFindings = 500;
}

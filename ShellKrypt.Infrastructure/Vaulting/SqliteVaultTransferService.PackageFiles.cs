namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static async Task WriteTextAsync(string path, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content, ct);
    }

    private static void EnsureFileSize(string path, long maxBytes, string label)
    {
        var fullPath = VaultFileGuard.NormalizeFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"{label} was not found.", fullPath);

        var bytes = new FileInfo(fullPath).Length;
        if (bytes > maxBytes)
            throw new InvalidOperationException($"{label} is too large. Limit: {FormatBytes(maxBytes)}.");
    }

    private static byte[] DecodeBase64Field(string value, string label)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{label} is not valid Base64.", ex);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        decimal display = bytes;
        var unitIndex = 0;
        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return $"{display:0.#} {units[unitIndex]}";
    }
}

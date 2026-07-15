namespace ShellKrypt.Core.DataTransfer;

public interface IVaultPlaintextExportService
{
    Task ExportJsonAsync(
        string vaultPath,
        byte[] vaultKey,
        string outputPath,
        CancellationToken ct = default);
}

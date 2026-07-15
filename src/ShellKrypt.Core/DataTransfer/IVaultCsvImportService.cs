namespace ShellKrypt.Core.DataTransfer;

public interface IVaultCsvImportService
{
    Task<VaultCsvImportPreview> PreviewAsync(
        string vaultPath,
        byte[] vaultKey,
        string csvPath,
        CancellationToken ct = default);

    Task ImportAsync(
        string vaultPath,
        byte[] vaultKey,
        string csvPath,
        VaultCsvDuplicateStrategy strategy,
        CancellationToken ct = default);
}

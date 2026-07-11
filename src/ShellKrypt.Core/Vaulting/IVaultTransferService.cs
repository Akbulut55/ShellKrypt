using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Core.Vaulting;

public interface IVaultTransferService
{
    Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default);
    Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default);
    Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default);
    Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default);
    Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default);
    Task ImportCsvAsync(string vaultPath, byte[] vaultKey, string csvPath, VaultCsvDuplicateStrategy strategy, CancellationToken ct = default);
}

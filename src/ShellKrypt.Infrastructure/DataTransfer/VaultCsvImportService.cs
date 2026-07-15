using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Infrastructure.DataTransfer.Internal;

namespace ShellKrypt.Infrastructure.DataTransfer;

public sealed class VaultCsvImportService : IVaultCsvImportService
{
    private readonly VaultCsvImportProcessor _processor = new();

    public Task<VaultCsvImportPreview> PreviewAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
        => _processor.PreviewCsvImportAsync(vaultPath, vaultKey, csvPath, ct);

    public Task ImportAsync(string vaultPath, byte[] vaultKey, string csvPath, VaultCsvDuplicateStrategy strategy, CancellationToken ct = default)
        => _processor.ImportCsvAsync(vaultPath, vaultKey, csvPath, strategy, ct);
}

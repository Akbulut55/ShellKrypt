namespace ShellKrypt.Application.Items;

public interface IVaultItemSummaryService
{
    Task<VaultItemSummaryResult> ListAsync(
        string vaultPath,
        byte[] vaultKey,
        ItemListQuery query,
        CancellationToken ct = default);
}

using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService : IVaultItemSummaryService
{
    private const int RecentWindowDays = 30;

    private readonly IItemRepository _repository;
    private readonly IVaultItemPayloadReader _payloadReader;
    private readonly Func<DateTimeOffset> _utcNow;

    public VaultItemSummaryService(
        IItemRepository repository,
        IVaultItemPayloadReader payloadReader,
        Func<DateTimeOffset>? utcNow = null)
    {
        _repository = repository;
        _payloadReader = payloadReader;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<VaultItemSummaryResult> ListAsync(
        string vaultPath,
        byte[] vaultKey,
        ItemListQuery query,
        CancellationToken ct = default)
    {
        var rows = await _repository.ListAsync(vaultPath, vaultKey, ct);
        var passwords = new List<string>();
        var all = rows
            .Where(row => row.Header.Type != ItemType.QuickFillEntry)
            .Select(row => BuildSummary(row, vaultKey, passwords))
            .ToArray();
        var counts = BuildCounts(all, passwords);
        var filtered = ApplyQuery(all, NormalizeQuery(query)).ToArray();
        var page = BuildPage(filtered, NormalizeQuery(query));

        return new VaultItemSummaryResult(all, page, counts);
    }
}

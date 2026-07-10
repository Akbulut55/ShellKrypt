using ShellKrypt.Application.Common;

namespace ShellKrypt.Application.Items;

public sealed record VaultItemSummaryResult(
    IReadOnlyList<VaultItemSummary> AllItems,
    PagedResult<VaultItemSummary> Page,
    VaultItemSummaryCounts Counts);

namespace ShellKrypt.Application.Items;

public sealed record VaultItemSummaryCounts(
    int Total,
    int WebLogins,
    int Cards,
    int Notes,
    int Authenticators,
    int ApiKeys,
    int WeakPasswords,
    int ReusedPasswords,
    int ExpiringSoonCards,
    int CreatedThisMonth);

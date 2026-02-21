namespace ShellKrypt.Core.Items;

public sealed record VaultItemHeader(
    string Id,
    ItemType Type,
    bool Favorite,
    string CreatedAtUtc,
    string UpdatedAtUtc
    );
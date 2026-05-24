using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed record VaultItemSummary(
    string Id,
    ItemType Type,
    string Title,
    string Subtitle,
    string Identifier,
    IReadOnlyList<string> Labels,
    string SearchText,
    bool Favorite,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    string CopyValue,
    int ExpiryMonth = 0,
    int ExpiryYear = 0)
{
    public bool IsCardExpiryUrgent(DateTime today)
    {
        if (Type != ItemType.Card || ExpiryMonth is < 1 or > 12 || ExpiryYear < 2000)
            return false;

        var expiry = new DateTime(ExpiryYear, ExpiryMonth, DateTime.DaysInMonth(ExpiryYear, ExpiryMonth));
        return expiry >= today && expiry <= today.AddMonths(3);
    }

    public bool IsRecent(DateTimeOffset nowUtc, int recentWindowDays)
    {
        return DateTimeOffset.TryParse(UpdatedAtUtc, out var updated)
               && updated.ToUniversalTime() >= nowUtc.AddDays(-recentWindowDays);
    }
}

using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummaryCounts BuildCounts(IReadOnlyList<VaultItemSummary> items, IReadOnlyList<string> webPasswords)
    {
        var now = _utcNow();
        return new VaultItemSummaryCounts(
            Total: items.Count,
            WebLogins: items.Count(item => item.Type == ItemType.Web),
            Cards: items.Count(item => item.Type == ItemType.Card),
            Notes: items.Count(item => item.Type == ItemType.Note),
            Authenticators: items.Count(item => item.Type == ItemType.Authenticator),
            ApiKeys: items.Count(item => item.Type == ItemType.ApiKey),
            WeakPasswords: webPasswords.Count(IsWeakPassword),
            ReusedPasswords: CountReusedPasswords(webPasswords),
            ExpiringSoonCards: items.Count(item => item.IsCardExpiryUrgent(now.LocalDateTime.Date)),
            CreatedThisMonth: items.Count(item => DateTimeOffset.TryParse(item.CreatedAtUtc, out var created)
                                                  && created.ToUniversalTime().Year == now.Year
                                                  && created.ToUniversalTime().Month == now.Month));
    }

    private static bool IsWeakPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return true;

        var value = password.Trim();
        if (value.Length < 12)
            return true;

        var hasLetter = value.Any(char.IsLetter);
        var hasDigit = value.Any(char.IsDigit);
        var hasSymbol = value.Any(ch => !char.IsLetterOrDigit(ch));
        return !(hasLetter && hasDigit && hasSymbol);
    }

    private static int CountReusedPasswords(IEnumerable<string> passwords)
    {
        return passwords
            .Where(password => !string.IsNullOrWhiteSpace(password))
            .GroupBy(password => password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count());
    }
}

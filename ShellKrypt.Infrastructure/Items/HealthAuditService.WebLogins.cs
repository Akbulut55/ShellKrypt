using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddWebLogin(VaultItemRow row, byte[] vaultKey, List<WebLoginHealthItem> webLogins)
    {
        var payload = JsonSerializer.Deserialize<WebPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        webLogins.Add(new WebLoginHealthItem(
            row.Header.Id,
            SafeName(payload.Title, "Web login"),
            payload.Username,
            payload.Password,
            ParseUpdated(row.Header.UpdatedAtUtc)));
    }

    private static void AddPasswordFindings(IReadOnlyList<WebLoginHealthItem> webLogins, List<HealthAuditIssue> issues)
    {
        var reusedPasswordIds = new HashSet<string>(StringComparer.Ordinal);
        var reusedGroups = webLogins
            .Where(item => !string.IsNullOrWhiteSpace(item.Password))
            .GroupBy(item => item.Password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in reusedGroups)
        {
            var affected = group.ToList();
            foreach (var item in affected)
            {
                reusedPasswordIds.Add(item.Id);
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.High,
                    HealthAuditCategory.ReusedPassword,
                    item.Title,
                    "Reused password",
                    $"This login shares its saved password with {affected.Count - 1} other web login(s). Use a unique generated password for this account.",
                    HealthAuditRecommendedAction.GenerateReplacementPassword);
            }
        }

        foreach (var item in webLogins)
        {
            if (string.IsNullOrWhiteSpace(item.Password))
            {
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.High,
                    HealthAuditCategory.EmptyPassword,
                    item.Title,
                    "Empty password",
                    "This login does not have a saved password. Add a unique password or remove the incomplete record.",
                    HealthAuditRecommendedAction.GenerateReplacementPassword);
            }
            else
            {
                var weaknesses = DescribeWeaknesses(item.Password);
                if (!string.IsNullOrWhiteSpace(weaknesses))
                {
                    AddIssue(
                        issues,
                        item.Id,
                        ItemType.Web,
                        reusedPasswordIds.Contains(item.Id) ? HealthAuditSeverity.High : HealthAuditSeverity.Medium,
                        HealthAuditCategory.WeakPassword,
                        item.Title,
                        "Weak password",
                        weaknesses,
                        HealthAuditRecommendedAction.GenerateReplacementPassword);
                }
            }

            var age = DateTimeOffset.UtcNow - item.UpdatedAtUtc;
            if (age.TotalDays >= OldPasswordDays)
            {
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.Low,
                    HealthAuditCategory.StaleCredential,
                    item.Title,
                    "Password not updated recently",
                    $"This login was last updated {FormatAge(age)} ago. Review whether the credential should be rotated.",
                    HealthAuditRecommendedAction.OpenWebLogin);
            }
        }
    }

    private static string DescribeWeaknesses(string password)
    {
        var issues = new List<string>();
        if (password.Length < 12)
            issues.Add("shorter than 12 characters");
        if (!password.Any(char.IsLower))
            issues.Add("missing lowercase");
        if (!password.Any(char.IsUpper))
            issues.Add("missing uppercase");
        if (!password.Any(char.IsDigit))
            issues.Add("missing digit");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            issues.Add("missing symbol");

        return issues.Count == 0
            ? ""
            : "This password is " + string.Join(", ", issues) + ". Use a unique generated password where possible.";
    }

    private sealed record WebLoginHealthItem(
        string Id,
        string Title,
        string Username,
        string Password,
        DateTimeOffset UpdatedAtUtc);
}

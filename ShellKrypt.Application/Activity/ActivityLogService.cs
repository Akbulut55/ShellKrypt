using System.Text.RegularExpressions;
using ShellKrypt.Application.Ports;

namespace ShellKrypt.Application.Activity;

public sealed partial class ActivityLogService
{
    private readonly IActivityLogStore _store;

    public ActivityLogService(IActivityLogStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ActivityLogEntry> Load(string? vaultPath = null, byte[]? vaultKey = null)
        => _store.Load(vaultPath, vaultKey);

    public void Append(ActivityLogEntry entry, byte[]? vaultKey = null)
        => _store.Append(SanitizeEntry(entry), vaultKey);

    public void Clear(string? vaultPath = null, byte[]? vaultKey = null)
        => _store.Clear(vaultPath, vaultKey);

    private static ActivityLogEntry SanitizeEntry(ActivityLogEntry entry)
        => entry with
        {
            Detail = SanitizeLogText(entry.Detail),
            AffectedItem = string.IsNullOrWhiteSpace(entry.AffectedItem) ? entry.AffectedItem : SanitizeLogText(entry.AffectedItem)
        };

    public static string SanitizeLogText(string value)
    {
        var sanitized = SensitiveAssignmentRegex().Replace(value ?? string.Empty, match => $"{match.Groups[1].Value}=[redacted]");
        return CardLikeNumberRegex().Replace(sanitized, "[redacted-number]");
    }

    [GeneratedRegex(@"\b(password|passphrase|secret|token|api[-_ ]?key|cvc|cvv)\s*[:=]\s*[^,\s;]+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){12,19}\b")]
    private static partial Regex CardLikeNumberRegex();
}

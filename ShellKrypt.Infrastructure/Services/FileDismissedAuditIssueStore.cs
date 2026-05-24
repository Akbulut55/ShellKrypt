using System.Text.Json;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Ports;

namespace ShellKrypt.Infrastructure.Services;

public sealed class FileDismissedAuditIssueStore : IAuditDismissalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<DismissedAuditIssueRecord> Load()
    {
        try
        {
            if (!File.Exists(DefaultPaths.AuditDismissalsPath))
                return [];

            return JsonSerializer.Deserialize<List<DismissedAuditIssueRecord>>(
                       File.ReadAllText(DefaultPaths.AuditDismissalsPath),
                       JsonOptions)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<DismissedAuditIssueRecord> records)
    {
        var dir = Path.GetDirectoryName(DefaultPaths.AuditDismissalsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(records, JsonOptions);
        File.WriteAllText(DefaultPaths.AuditDismissalsPath, json);
    }
}

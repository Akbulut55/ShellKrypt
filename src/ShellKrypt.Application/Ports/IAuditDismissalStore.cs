using ShellKrypt.Application.Audit;

namespace ShellKrypt.Application.Ports;

public interface IAuditDismissalStore
{
    IReadOnlyList<DismissedAuditIssueRecord> Load();
    void Save(IReadOnlyList<DismissedAuditIssueRecord> records);
}

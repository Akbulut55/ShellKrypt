namespace ShellKrypt.Application.Audit;

public sealed record DismissedAuditIssueRecord(
    string VaultPath,
    string Fingerprint,
    string DismissedAtUtc);

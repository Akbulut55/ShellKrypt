namespace ShellKrypt.Core.Items;

public sealed record AuthenticatorPayload(
    string ServiceName,
    string Issuer,
    string AccountLabel,
    string Secret,
    string Algorithm,
    int Digits,
    int PeriodSeconds,
    string RecoveryNotes,
    string LastUsedAtUtc,
    string KeyType = "",
    long Counter = 0
);

namespace ShellKrypt.Core.Authenticator;

public enum AuthenticatorKeyType
{
    TimeBased = 1,
    CounterBased = 2
}

public sealed record AuthenticatorInput(
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter = 0,
    string Algorithm = "HMAC-SHA1",
    int Digits = 6,
    int PeriodSeconds = 30);

public sealed record AuthenticatorEntry(
    string Id,
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter,
    string Algorithm,
    int Digits,
    int PeriodSeconds,
    string LastUsedAtUtc,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public sealed record AuthenticatorCodeSnapshot(
    string Code,
    int SecondsRemaining,
    double ProgressPercent,
    bool IsValid);

public sealed record ParsedOtpAuthSecret(
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter,
    string Algorithm,
    int Digits,
    int PeriodSeconds);

namespace ShellKrypt.Application.Session;

public sealed record VaultSessionState(string? VaultPath, byte[]? VaultKey, bool IsUnlocked);

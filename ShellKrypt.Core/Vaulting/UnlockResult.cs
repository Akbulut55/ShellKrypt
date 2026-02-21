namespace ShellKrypt.Core.Vaulting;

public sealed record UnlockResult(bool Success, string? Error, byte[]? VaultKey)
{
    public static UnlockResult Ok(byte[] vaultKey) => new(true, null, vaultKey);
    public static UnlockResult Fail(string error) => new(false, error, null);
}
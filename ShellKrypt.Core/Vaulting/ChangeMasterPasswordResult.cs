namespace ShellKrypt.Core.Vaulting;

public sealed record ChangeMasterPasswordResult(bool Success, string? Error = null)
{
    public static ChangeMasterPasswordResult Ok()
        => new(true);

    public static ChangeMasterPasswordResult Fail(string error)
        => new(false, error);
}

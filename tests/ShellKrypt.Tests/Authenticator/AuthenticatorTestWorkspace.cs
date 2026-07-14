using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Tests.Authenticator;

internal sealed class AuthenticatorTestWorkspace : IDisposable
{
    internal AuthenticatorTestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    internal string Root { get; }
    internal string FilePath(string fileName) => Path.Combine(Root, fileName);

    internal static async Task<byte[]> CreateAndUnlockVaultAsync(SqliteVaultService vaultService, string vaultPath)
    {
        const string password = "Vault Master Passphrase 2026";
        await vaultService.CreateAsync(vaultPath, password);
        var result = await vaultService.UnlockAsync(vaultPath, password);
        return result.Success && result.VaultKey is not null
            ? result.VaultKey
            : throw new InvalidOperationException(result.Error ?? "Unable to unlock vault.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
        catch
        {
        }
    }
}

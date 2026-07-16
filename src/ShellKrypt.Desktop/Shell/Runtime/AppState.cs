using System;
using System.Security.Cryptography;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class AppState
{
    public string? VaultPath { get; set; }
    public byte[]? VaultKey { get; set; }

    public byte[] GetVaultKeyOrThrow()
        => VaultKey ?? throw new InvalidOperationException("Vault is locked.");

    public void ClearSensitive()
    {
        if (VaultKey is not null)
            CryptographicOperations.ZeroMemory(VaultKey);

        VaultKey = null;
    }
}
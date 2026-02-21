using System;
using System.Security.Cryptography;

namespace ShellKrypt.Desktop.Services;

public sealed class AppState
{
    public string? VaultPath { get; set; }
    public byte[]? VaultKey { get; set; }

    public void ClearSensitive()
    {
        if (VaultKey is not null)
            CryptographicOperations.ZeroMemory(VaultKey);

        VaultKey = null;
    }
}
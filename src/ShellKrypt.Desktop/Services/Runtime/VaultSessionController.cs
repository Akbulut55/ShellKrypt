using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.Services.Runtime;

public sealed class VaultSessionController(AppState state) : IVaultSessionController
{
    public string? VaultPath => state.VaultPath;
    public bool IsUnlocked => state.VaultKey is not null;
    public byte[] VaultKey => state.GetVaultKeyOrThrow();

    public event EventHandler? StateChanged;

    public void SetVaultPath(string? path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        if (string.Equals(state.VaultPath, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        state.VaultPath = normalized;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetVaultKey(byte[] vaultKey)
    {
        ArgumentNullException.ThrowIfNull(vaultKey);
        state.VaultKey = vaultKey;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSensitive()
    {
        state.ClearSensitive();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

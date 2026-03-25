using System.Collections.Generic;

namespace ShellKrypt.Desktop.Services;

public sealed class VaultRegistry
{
    public List<VaultRegistryEntry> Vaults { get; set; } = new();
}

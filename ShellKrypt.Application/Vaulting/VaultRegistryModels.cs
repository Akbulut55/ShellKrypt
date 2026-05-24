namespace ShellKrypt.Application.Vaulting;

public sealed class VaultRegistry
{
    public List<VaultRegistryEntry> Vaults { get; set; } = new();
}

public sealed class VaultRegistryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VaultPath { get; set; } = "";
    public string DisplayName { get; set; } = "Vault";
    public string Description { get; set; } = string.Empty;
    public string CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? LastOpenedAtUtc { get; set; }
    public bool IsDefault { get; set; }
}

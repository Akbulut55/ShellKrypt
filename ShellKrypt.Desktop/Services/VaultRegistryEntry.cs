using System;

namespace ShellKrypt.Desktop.Services;

public sealed class VaultRegistryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VaultPath { get; set; } = "";
    public string DisplayName { get; set; } = "Vault";
    public string Description { get; set; } = "";
    public string? AccentColor { get; set; }
    public string? IconKey { get; set; }
    public string CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? LastOpenedAtUtc { get; set; }
    public bool IsDefault { get; set; }
}

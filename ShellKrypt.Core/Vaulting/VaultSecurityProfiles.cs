using System.Collections.Generic;
using System.Linq;

namespace ShellKrypt.Core.Vaulting;

public sealed record VaultSecurityProfile(
    string Key,
    string Label,
    string Description,
    VaultKdfParams Kdf);

public static class VaultSecurityProfiles
{
    public static IReadOnlyList<VaultSecurityProfile> All { get; } =
    [
        new(
            Key: "balanced",
            Label: "Balanced",
            Description: "Good default for most devices.",
            Kdf: new VaultKdfParams(MemoryKb: 65536, Iterations: 3, Parallelism: 2)),
        new(
            Key: "hardened",
            Label: "Hardened",
            Description: "Higher memory cost for stronger offline-attack resistance.",
            Kdf: new VaultKdfParams(MemoryKb: 131072, Iterations: 4, Parallelism: 2)),
        new(
            Key: "maximum",
            Label: "Maximum",
            Description: "Best resistance, but slower on weaker systems.",
            Kdf: new VaultKdfParams(MemoryKb: 262144, Iterations: 4, Parallelism: 2)),
    ];

    public static VaultSecurityProfile Default => All[1];

    public static VaultSecurityProfile FromKey(string? key)
        => All.FirstOrDefault(profile => string.Equals(profile.Key, key, System.StringComparison.OrdinalIgnoreCase))
           ?? Default;

    public static VaultSecurityProfile? Match(VaultKdfParams kdf)
        => All.FirstOrDefault(profile =>
            profile.Kdf.MemoryKb == kdf.MemoryKb &&
            profile.Kdf.Iterations == kdf.Iterations &&
            profile.Kdf.Parallelism == kdf.Parallelism);
}

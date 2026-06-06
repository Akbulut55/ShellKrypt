using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService : IVaultService
{
    private const int Version = 1;

    private const int KeySize = 32;
    private const int SaltSize = 16;

    private static VaultKdfParams DefaultKdf()
        => VaultKdfPolicy.Normalize(VaultSecurityProfiles.Default.Kdf);
}

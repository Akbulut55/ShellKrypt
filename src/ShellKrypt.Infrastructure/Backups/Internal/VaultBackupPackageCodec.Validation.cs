using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class VaultBackupPackageCodec
{
    private static void ValidatePackageMetadata(VaultEncryptedPackage package)
    {
        if (package.Version != VaultBackupPackageCodec.CurrentVersion)
            throw new NotSupportedException($"Unsupported package version {package.Version}.");

        if (!VaultKdfPolicy.IsValidStored(package.Kdf, out var kdfError))
            throw new InvalidOperationException(kdfError);
    }

}

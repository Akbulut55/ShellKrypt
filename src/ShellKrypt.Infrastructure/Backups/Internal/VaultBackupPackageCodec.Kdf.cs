using System.Text;
using Konscious.Security.Cryptography;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class VaultBackupPackageCodec
{
    private static VaultKdfParams DefaultKdf()
    {
        var p = Math.Max(1, Environment.ProcessorCount / 2);
        return VaultKdfPolicy.Normalize(new VaultKdfParams(65536, 3, p));
    }

    private static Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, VaultKdfParams p, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(passphrase))
            {
                Salt = salt,
                MemorySize = p.MemoryKb,
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Parallelism
            };

            return argon2.GetBytes(KeySize);
        }, ct);
    }
}

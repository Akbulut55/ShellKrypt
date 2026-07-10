using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService
{
    private static Task<byte[]> DeriveKeyAsync(string masterPassword, byte[] salt, VaultKdfParams p, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
            {
                Salt = salt,
                MemorySize = p.MemoryKb,        // KB
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Parallelism
            };
            return argon2.GetBytes(KeySize);
        }, ct);
    }
}

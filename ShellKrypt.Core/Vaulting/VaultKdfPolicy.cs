namespace ShellKrypt.Core.Vaulting;

public static class VaultKdfPolicy
{
    public const int MinMemoryKb = 32768;
    public const int MaxMemoryKb = 1048576;
    public const int MinIterations = 3;
    public const int MaxIterations = 20;
    public const int MinParallelism = 1;
    public const int MaxParallelism = 64;

    public static VaultKdfParams Normalize(VaultKdfParams p)
    {
        var maxParallelism = Math.Clamp(Environment.ProcessorCount, MinParallelism, MaxParallelism);
        return new VaultKdfParams(
            MemoryKb: Math.Clamp(p.MemoryKb, MinMemoryKb, MaxMemoryKb),
            Iterations: Math.Clamp(p.Iterations, MinIterations, MaxIterations),
            Parallelism: Math.Clamp(p.Parallelism, MinParallelism, maxParallelism));
    }

    public static bool IsValidStored(VaultKdfParams p, out string error)
    {
        if (p.MemoryKb is < MinMemoryKb or > MaxMemoryKb)
        {
            error = "Vault KDF memory setting is outside the supported range.";
            return false;
        }

        if (p.Iterations is < MinIterations or > MaxIterations)
        {
            error = "Vault KDF iteration setting is outside the supported range.";
            return false;
        }

        if (p.Parallelism is < MinParallelism or > MaxParallelism)
        {
            error = "Vault KDF parallelism setting is outside the supported range.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

namespace ShellKrypt.Core.Vaulting;

public sealed record VaultKdfParams(int MemoryKb, int Iterations, int Parallelism);
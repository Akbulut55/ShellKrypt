namespace ShellKrypt.Core.ProjectSecrets;

public interface IProjectSecretService
{
    Task<IReadOnlyList<ProjectSecretEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<ProjectSecretEntry> AddAsync(string vaultPath, byte[] vaultKey, ProjectSecretInput input, CancellationToken ct = default);
    Task<ProjectSecretEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, ProjectSecretInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed partial class ProjectSecretService
{
    public async Task<IReadOnlyList<ProjectSecretEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var projects = new List<ProjectSecretEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.ProjectSecret))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            projects.Add(ToEntry(row.Header, payload));
        }

        return projects;
    }
}

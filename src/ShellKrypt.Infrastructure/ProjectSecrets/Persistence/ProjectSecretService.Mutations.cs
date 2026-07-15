using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed partial class ProjectSecretService
{
    public async Task<ProjectSecretEntry> AddAsync(
        string vaultPath,
        byte[] vaultKey,
        ProjectSecretInput input,
        CancellationToken ct = default)
    {
        await EnsureUniqueNameAsync(vaultPath, vaultKey, input.Name, null, ct);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.ProjectSecret,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<ProjectSecretEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        ProjectSecretInput input,
        CancellationToken ct = default)
    {
        await EnsureUniqueNameAsync(vaultPath, vaultKey, input.Name, id, ct);
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.ProjectSecret,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);

    private async Task EnsureUniqueNameAsync(string vaultPath, byte[] vaultKey, string name, string? excludedId, CancellationToken ct)
    {
        var normalized = NormalizeRequired(name, "Project name is required.");
        var projects = await ListAsync(vaultPath, vaultKey, ct);
        if (projects.Any(project => project.Id != excludedId && string.Equals(project.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Project name already exists.");
    }
}

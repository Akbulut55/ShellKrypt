using System.Text;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private async Task<VaultSnapshot> BuildSnapshotAsync(string vaultPath, byte[] vaultKey, CancellationToken ct)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var labels = await _repo.ListLabelsAsync(vaultPath, vaultKey, ct);

        var items = new List<VaultSnapshotItem>(rows.Count);
        var itemLabels = new List<VaultSnapshotItemLabel>();

        foreach (var row in rows)
        {
            var payloadJson = Encoding.UTF8.GetString(VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload));
            items.Add(new VaultSnapshotItem(
                row.Header.Id,
                row.Header.Type,
                row.Header.Favorite,
                row.Header.CreatedAtUtc,
                row.Header.UpdatedAtUtc,
                payloadJson));

            foreach (var label in row.Labels)
                itemLabels.Add(new VaultSnapshotItemLabel(row.Header.Id, label.Id));
        }

        var snapshotLabels = labels
            .Select(x => new VaultSnapshotLabel(x.Id, x.Name, x.Color))
            .ToArray();

        return new VaultSnapshot(PackageVersion, DateTimeOffset.UtcNow.ToString("O"), items, snapshotLabels, itemLabels);
    }

    private static VaultSnapshotSummary Summarize(VaultSnapshot snapshot)
    {
        return new VaultSnapshotSummary(
            snapshot.Items.Count,
            snapshot.Items.Count(x => x.Type == ItemType.Web),
            snapshot.Items.Count(x => x.Type == ItemType.Card),
            snapshot.Items.Count(x => x.Type == ItemType.Note),
            snapshot.Items.Count(x => x.Type == ItemType.Authenticator),
            snapshot.Items.Count(x => x.Type == ItemType.ApiKey),
            snapshot.Labels.Count,
            snapshot.Items.Count(x => x.Favorite));
    }
}

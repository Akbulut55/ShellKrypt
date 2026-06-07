using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
    public async Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var items = new Dictionary<string, ItemRowBuilder>(StringComparer.Ordinal);
        var order = new List<string>();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT i.id, i.type, i.favorite, i.createdAtUtc, i.updatedAtUtc, i.encryptedPayload,
               l.id, l.encryptedName, l.name, l.color
        FROM items i
        LEFT JOIN item_labels il ON il.itemId = i.id
        LEFT JOIN labels l ON l.id = il.labelId
        ORDER BY i.updatedAtUtc DESC, i.id ASC;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);

            if (!items.TryGetValue(id, out var builder))
            {
                builder = new ItemRowBuilder(
                    new VaultItemHeader(
                        id,
                        (ItemType)reader.GetInt32(1),
                        reader.GetInt32(2) != 0,
                        reader.GetString(3),
                        reader.GetString(4)),
                    reader.GetFieldValue<byte[]>(5));

                items[id] = builder;
                order.Add(id);
            }

            if (!reader.IsDBNull(6))
            {
                builder.Labels.Add(new VaultLabelRow(
                    reader.GetString(6),
                    VaultPayloadProtector.DecryptLabelName(vaultKey, reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<byte[]>(7), reader.IsDBNull(8) ? null : reader.GetString(8)),
                    reader.IsDBNull(9) ? null : reader.GetString(9)));
            }
        }

        return order
            .Select(id => items[id].Build())
            .Select(SortLabels)
            .ToArray();
    }

    private static VaultItemRow SortLabels(VaultItemRow row)
        => new(
            row.Header,
            row.EncryptedPayload,
            row.Labels.OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    private sealed class ItemRowBuilder
    {
        public ItemRowBuilder(VaultItemHeader header, byte[] payload)
        {
            Header = header;
            Payload = payload;
        }

        public VaultItemHeader Header { get; }
        public byte[] Payload { get; }
        public List<VaultLabelRow> Labels { get; } = new();

        public VaultItemRow Build() => new(Header, Payload, Labels.ToArray());
    }
}

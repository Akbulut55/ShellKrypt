using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed class SqliteVaultTransferService : IVaultTransferService
{
    private const int PackageVersion = 1;
    private const int KeySize = 32;
    private const int SaltSize = 16;
    private const long MaxEncryptedPackageBytes = 64L * 1024 * 1024;
    private const long MaxCsvBytes = 8L * 1024 * 1024;
    private const int MaxSnapshotJsonBytes = 64 * 1024 * 1024;
    private const int MaxSnapshotItems = 10000;
    private const int MaxSnapshotLabels = 2000;
    private const int MaxSnapshotItemLabels = 50000;
    private const int MaxPayloadJsonChars = 1024 * 1024;
    private const int MaxCsvRows = 10000;
    private const int MaxCsvColumns = 64;
    private const int MaxCsvFieldChars = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };

    public async Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => Summarize(await BuildSnapshotAsync(vaultPath, vaultKey, ct));

    public async Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Plaintext export");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.JsonExtension, "Plaintext export");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions), ct);
    }

    public async Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Encrypted backup");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.BackupExtension, "Encrypted backup");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var package = await CreateEncryptedPackageAsync(snapshotBytes, exportPassphrase, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(package, JsonOptions), ct);
    }

    public async Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default)
        => Summarize(await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct));

    public async Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(packagePath, vaultPath, "Encrypted backup import cannot read from the active vault file.");
        var snapshot = await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct);
        await ImportSnapshotAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
    {
        ValidateSnapshot(snapshot);
        await ImportSnapshotTransactionalAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(csvPath, vaultPath, "CSV import cannot read from the active vault file.");
        csvPath = VaultFileGuard.EnsureExtension(csvPath, VaultFileGuard.CsvExtension, "CSV import file");
        EnsureFileSize(csvPath, MaxCsvBytes, "CSV import file");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var existingKeys = BuildDuplicateKeySet(snapshot);
        var csvText = await File.ReadAllTextAsync(csvPath, ct);
        var candidates = ParseCsvCandidates(csvText);

        var seenKeys = new HashSet<string>(existingKeys, StringComparer.Ordinal);
        var rows = new List<VaultCsvImportRowPreview>();
        var newRows = 0;
        var duplicateRows = 0;
        var invalidRows = 0;

        foreach (var candidate in candidates)
        {
            if (!candidate.IsValid)
            {
                rows.Add(candidate.ToPreview(VaultCsvRowStatus.Invalid, candidate.Error ?? "Invalid row."));
                invalidRows++;
                continue;
            }

            if (!seenKeys.Add(candidate.DuplicateKey))
            {
                rows.Add(candidate.ToPreview(VaultCsvRowStatus.Duplicate, "Duplicate item."));
                duplicateRows++;
                continue;
            }

            rows.Add(candidate.ToPreview(VaultCsvRowStatus.New, null));
            newRows++;
        }

        return new VaultCsvImportPreview(candidates.Count, newRows, duplicateRows, invalidRows, rows);
    }

    public async Task ImportCsvAsync(string vaultPath, byte[] vaultKey, string csvPath, VaultCsvDuplicateStrategy strategy, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(csvPath, vaultPath, "CSV import cannot read from the active vault file.");
        csvPath = VaultFileGuard.EnsureExtension(csvPath, VaultFileGuard.CsvExtension, "CSV import file");
        EnsureFileSize(csvPath, MaxCsvBytes, "CSV import file");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var duplicateKeyToId = BuildDuplicateKeyMap(snapshot);
        var csvText = await File.ReadAllTextAsync(csvPath, ct);
        var candidates = ParseCsvCandidates(csvText);

        var seenKeys = new HashSet<string>(duplicateKeyToId.Keys, StringComparer.Ordinal);
        var actions = new List<CsvImportAction>();

        foreach (var candidate in candidates)
        {
            if (!candidate.IsValid)
                continue;

            var duplicateDetected = seenKeys.Contains(candidate.DuplicateKey);
            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.SkipDuplicates)
                continue;

            string? deleteItemId = null;
            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.OverwriteDuplicates && duplicateKeyToId.TryGetValue(candidate.DuplicateKey, out var existingId))
            {
                deleteItemId = existingId;
                duplicateKeyToId.Remove(candidate.DuplicateKey);
                seenKeys.Remove(candidate.DuplicateKey);
            }

            actions.Add(new CsvImportAction(candidate, deleteItemId));
            duplicateKeyToId[candidate.DuplicateKey] = candidate.Id;
            seenKeys.Add(candidate.DuplicateKey);
        }

        await ImportCsvTransactionalAsync(vaultPath, vaultKey, actions, ct);
    }

    private readonly IItemRepository _repo = new SqliteItemRepository();

    private static async Task ImportSnapshotTransactionalAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct)
    {
        vaultPath = VaultFileGuard.EnsureExistingVaultFile(vaultPath);
        await using var conn = await OpenVaultConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            var labelMap = await UpsertSnapshotLabelsAsync(conn, tx, vaultKey, snapshot.Labels, ct);
            var existingItemIds = await ReadItemIdsAsync(conn, tx, ct);

            foreach (var item in snapshot.Items)
            {
                if (existingItemIds.Contains(item.Id))
                    await DeleteItemAsync(conn, tx, item.Id, ct);

                var header = new VaultItemHeader(item.Id, item.Type, item.Favorite, item.CreatedAtUtc, item.UpdatedAtUtc);
                await InsertItemAsync(conn, tx, vaultKey, header, item.PayloadJson, ct);
                existingItemIds.Add(item.Id);
            }

            foreach (var item in snapshot.Items)
            {
                var labelIds = snapshot.ItemLabels
                    .Where(x => x.ItemId == item.Id)
                    .Select(x => labelMap.TryGetValue(x.LabelId, out var mappedId) ? mappedId : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var labelId in labelIds)
                    await InsertItemLabelAsync(conn, tx, item.Id, labelId, ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task ImportCsvTransactionalAsync(string vaultPath, byte[] vaultKey, IReadOnlyList<CsvImportAction> actions, CancellationToken ct)
    {
        if (actions.Count == 0)
            return;

        vaultPath = VaultFileGuard.EnsureExistingVaultFile(vaultPath);
        await using var conn = await OpenVaultConnectionAsync(vaultPath, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action.DeleteItemId))
                    await DeleteItemAsync(conn, tx, action.DeleteItemId, ct);

                var candidate = action.Candidate;
                var header = new VaultItemHeader(candidate.Id, candidate.Type, false, candidate.CreatedAtUtc, candidate.UpdatedAtUtc);
                await InsertItemAsync(conn, tx, vaultKey, header, candidate.PayloadJson, ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<Dictionary<string, string>> UpsertSnapshotLabelsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        byte[] vaultKey,
        IReadOnlyList<VaultSnapshotLabel> labels,
        CancellationToken ct)
    {
        var labelMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var existing = await ReadStoredLabelsAsync(conn, tx, ct);

        foreach (var label in labels)
        {
            var normalized = NormalizeLabelName(label.Name);
            if (normalized is null)
                continue;

            var match = existing.FirstOrDefault(row =>
                string.Equals(
                    NormalizeLabelName(VaultPayloadProtector.DecryptLabelName(vaultKey, row.Id, row.EncryptedName, row.LegacyName)),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                labelMap[label.Id] = match.Id;
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
            INSERT INTO labels (id, encryptedName, name, color)
            VALUES ($id, $encryptedName, $lookup, $color);
            """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.Add("$encryptedName", SqliteType.Blob).Value = VaultPayloadProtector.EncryptLabelName(vaultKey, id, normalized);
            insert.Parameters.AddWithValue("$lookup", ComputeLabelLookupKey(normalized));
            insert.Parameters.AddWithValue("$color", string.IsNullOrWhiteSpace(label.Color) ? DBNull.Value : label.Color);
            await insert.ExecuteNonQueryAsync(ct);

            existing.Add(new StoredLabelRow(id, VaultPayloadProtector.EncryptLabelName(vaultKey, id, normalized), ComputeLabelLookupKey(normalized), label.Color));
            labelMap[label.Id] = id;
        }

        return labelMap;
    }

    private static async Task<HashSet<string>> ReadItemIdsAsync(SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM items;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }

    private static async Task DeleteItemAsync(SqliteConnection conn, SqliteTransaction tx, string itemId, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", itemId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertItemAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        byte[] vaultKey,
        VaultItemHeader header,
        string payloadJson,
        CancellationToken ct)
    {
        var encryptedPayload = VaultPayloadProtector.EncryptItemPayload(vaultKey, header, Encoding.UTF8.GetBytes(payloadJson));
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
        INSERT INTO items (id, type, favorite, createdAtUtc, updatedAtUtc, encryptedPayload)
        VALUES ($id, $type, $fav, $created, $updated, $payload);
        """;
        cmd.Parameters.AddWithValue("$id", header.Id);
        cmd.Parameters.AddWithValue("$type", (int)header.Type);
        cmd.Parameters.AddWithValue("$fav", header.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", header.CreatedAtUtc);
        cmd.Parameters.AddWithValue("$updated", header.UpdatedAtUtc);
        cmd.Parameters.Add("$payload", SqliteType.Blob).Value = encryptedPayload;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertItemLabelAsync(SqliteConnection conn, SqliteTransaction tx, string itemId, string labelId, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
        INSERT OR IGNORE INTO item_labels (itemId, labelId)
        VALUES ($itemId, $labelId);
        """;
        cmd.Parameters.AddWithValue("$itemId", itemId);
        cmd.Parameters.AddWithValue("$labelId", labelId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<SqliteConnection> OpenVaultConnectionAsync(string vaultPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }

    private static async Task EnsureLabelSchemaAsync(SqliteConnection conn, byte[] vaultKey, CancellationToken ct)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(labels);";
        await using (var reader = await pragma.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("encryptedName"))
        {
            var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE labels ADD COLUMN encryptedName BLOB;";
            await alter.ExecuteNonQueryAsync(ct);
        }

        var rows = await ReadStoredLabelsAsync(conn, null, ct);
        foreach (var row in rows.Where(row => row.EncryptedName is null && !string.IsNullOrWhiteSpace(row.LegacyName)))
        {
            var update = conn.CreateCommand();
            update.CommandText = """
            UPDATE labels
            SET encryptedName = $encryptedName,
                name = $lookup
            WHERE id = $id;
            """;
            update.Parameters.AddWithValue("$id", row.Id);
            update.Parameters.Add("$encryptedName", SqliteType.Blob).Value = VaultPayloadProtector.EncryptLabelName(vaultKey, row.Id, row.LegacyName!);
            update.Parameters.AddWithValue("$lookup", ComputeLabelLookupKey(row.LegacyName!));
            await update.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<List<StoredLabelRow>> ReadStoredLabelsAsync(SqliteConnection conn, SqliteTransaction? tx, CancellationToken ct)
    {
        var labels = new List<StoredLabelRow>();
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, encryptedName, name, color FROM labels ORDER BY id ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            labels.Add(new StoredLabelRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return labels;
    }

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

    private static async Task WriteTextAsync(string path, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content, ct);
    }

    private static async Task<VaultEncryptedPackage> CreateEncryptedPackageAsync(byte[] plaintext, string passphrase, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Export passphrase is required.", nameof(passphrase));

        if (plaintext.Length > MaxSnapshotJsonBytes)
            throw new InvalidOperationException("Vault snapshot is too large to export.");

        var kdf = DefaultKdf();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derivedKey = await DeriveKeyAsync(passphrase, salt, kdf, ct);
        try
        {
            var encrypted = AesGcmBlob.Encrypt(derivedKey, plaintext, BackupAssociatedData());
            return new VaultEncryptedPackage(
                PackageVersion,
                DateTimeOffset.UtcNow.ToString("O"),
                kdf,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(encrypted));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static void ValidateSnapshot(VaultSnapshot snapshot)
    {
        if (snapshot.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported snapshot version {snapshot.Version}.");

        if (snapshot.Items.Count > MaxSnapshotItems)
            throw new InvalidOperationException($"Snapshot contains too many items. Limit: {MaxSnapshotItems}.");

        if (snapshot.Labels.Count > MaxSnapshotLabels)
            throw new InvalidOperationException($"Snapshot contains too many labels. Limit: {MaxSnapshotLabels}.");

        if (snapshot.ItemLabels.Count > MaxSnapshotItemLabels)
            throw new InvalidOperationException($"Snapshot contains too many item-label links. Limit: {MaxSnapshotItemLabels}.");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new InvalidOperationException("Snapshot contains an item without an id.");

            if (!itemIds.Add(item.Id))
                throw new InvalidOperationException("Snapshot contains duplicate item ids.");

            if (item.PayloadJson.Length > MaxPayloadJsonChars)
                throw new InvalidOperationException("Snapshot contains an item payload that is too large.");

            _ = BuildDuplicateKey(item.Type, item.PayloadJson);
        }

        var labelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in snapshot.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Id))
                throw new InvalidOperationException("Snapshot contains a label without an id.");

            if (!labelIds.Add(label.Id))
                throw new InvalidOperationException("Snapshot contains duplicate label ids.");

            if ((label.Name?.Length ?? 0) > MaxCsvFieldChars)
                throw new InvalidOperationException("Snapshot contains a label name that is too large.");
        }
    }

    private static async Task<VaultSnapshot> ReadEncryptedSnapshotAsync(string packagePath, string passphrase, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Import passphrase is required.", nameof(passphrase));

        packagePath = VaultFileGuard.EnsureExtension(packagePath, VaultFileGuard.BackupExtension, "Encrypted backup file");
        EnsureFileSize(packagePath, MaxEncryptedPackageBytes, "Encrypted backup file");
        var json = await File.ReadAllTextAsync(packagePath, ct);
        var package = JsonSerializer.Deserialize<VaultEncryptedPackage>(json, JsonOptions)
            ?? throw new InvalidOperationException("Encrypted export file is empty or invalid.");

        if (package.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported package version {package.Version}.");

        if (!VaultKdfPolicy.IsValidStored(package.Kdf, out var kdfError))
            throw new InvalidOperationException(kdfError);

        var salt = DecodeBase64Field(package.SaltBase64, "Backup salt");
        if (salt.Length != SaltSize)
            throw new InvalidOperationException("Backup salt is invalid.");

        var encrypted = DecodeBase64Field(package.CiphertextBase64, "Backup ciphertext");
        if (encrypted.Length < AesGcmBlob.NonceSize + AesGcmBlob.TagSize)
            throw new InvalidOperationException("Backup ciphertext is invalid.");

        var derivedKey = await DeriveKeyAsync(passphrase, salt, package.Kdf, ct);
        try
        {
            var plaintext = AesGcmBlob.Decrypt(derivedKey, encrypted, BackupAssociatedData());
            if (plaintext.Length > MaxSnapshotJsonBytes)
                throw new InvalidOperationException("Encrypted export payload is too large.");

            var snapshot = JsonSerializer.Deserialize<VaultSnapshot>(plaintext, JsonOptions)
                ?? throw new InvalidOperationException("Encrypted export payload is empty or invalid.");
            ValidateSnapshot(snapshot);
            return snapshot;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static void EnsureFileSize(string path, long maxBytes, string label)
    {
        var fullPath = VaultFileGuard.NormalizeFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"{label} was not found.", fullPath);

        var bytes = new FileInfo(fullPath).Length;
        if (bytes > maxBytes)
            throw new InvalidOperationException($"{label} is too large. Limit: {FormatBytes(maxBytes)}.");
    }

    private static byte[] DecodeBase64Field(string value, string label)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{label} is not valid Base64.", ex);
        }
    }

    private static byte[] BackupAssociatedData()
        => AesGcmBlob.CreateAssociatedData("vault-backup", "v1");

    private static string? NormalizeLabelName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ComputeLabelLookupKey(string name)
    {
        var normalized = NormalizeLabelName(name) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        decimal display = bytes;
        var unitIndex = 0;
        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return $"{display:0.#} {units[unitIndex]}";
    }

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

    private static HashSet<string> BuildDuplicateKeySet(VaultSnapshot snapshot)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
            set.Add(BuildDuplicateKey(item.Type, item.PayloadJson));
        return set;
    }

    private static Dictionary<string, string> BuildDuplicateKeyMap(VaultSnapshot snapshot)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
            map[BuildDuplicateKey(item.Type, item.PayloadJson)] = item.Id;
        return map;
    }

    private static string BuildDuplicateKey(ItemType type, string payloadJson)
    {
        return type switch
        {
            ItemType.Web => BuildWebDuplicateKey(payloadJson),
            ItemType.Card => BuildCardDuplicateKey(payloadJson),
            ItemType.Note => BuildNoteDuplicateKey(payloadJson),
            ItemType.Authenticator => BuildAuthenticatorDuplicateKey(payloadJson),
            ItemType.ApiKey => BuildApiKeyDuplicateKey(payloadJson),
            _ => $"{(int)type}|{payloadJson.Trim()}"
        };
    }

    private static string BuildWebDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<WebPayload>(payloadJson, JsonOptions)
            ?? new WebPayload("", "", "", "", "");
        return string.Join("|",
            "web",
            NormalizeDuplicatePart(payload.Title),
            NormalizeDuplicatePart(payload.Username),
            NormalizeDuplicatePart(payload.Url));
    }

    private static string BuildCardDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<CardPayload>(payloadJson, JsonOptions)
            ?? new CardPayload("", "", "", 0, 0, "", "");
        return string.Join("|",
            "card",
            NormalizeDuplicatePart(payload.Title),
            NormalizeDuplicatePart(payload.Cardholder),
            Last4(payload.Number));
    }

    private static string BuildApiKeyDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<ApiKeyPayload>(payloadJson, JsonOptions)
            ?? new ApiKeyPayload("", "", "", "", Array.Empty<ApiKeyFieldPayload>());

        return string.Join("|",
            "api",
            NormalizeDuplicatePart(payload.Name),
            NormalizeDuplicatePart(payload.Provider),
            NormalizeDuplicatePart(payload.Environment));
    }

    private static string BuildNoteDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<NotePayload>(payloadJson, JsonOptions)
            ?? new NotePayload("", "");
        return string.Join("|", "note", NormalizeDuplicatePart(payload.Title));
    }

    private static string BuildAuthenticatorDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<AuthenticatorPayload>(payloadJson, JsonOptions)
            ?? new AuthenticatorPayload("", "", "", "", "", 6, 30, "", "", "", 0);
        return string.Join("|",
            "authenticator",
            NormalizeDuplicatePart(payload.ServiceName),
            NormalizeDuplicatePart(payload.KeyType));
    }

    private static string NormalizeDuplicatePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();

    private static string Last4(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return digits;

        return digits[^4..];
    }

    private static List<CsvCandidate> ParseCsvCandidates(string csvText)
    {
        var records = ParseCsvRecords(csvText);
        if (records.Count == 0)
            return [];

        var headers = records[0];
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i].Trim();
            if (!string.IsNullOrWhiteSpace(header) && !index.ContainsKey(header))
                index[header] = i;
        }

        var candidates = new List<CsvCandidate>();
        for (var rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            var record = records[rowIndex];
            var candidate = ParseCsvCandidate(record, index, rowIndex + 1);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        return candidates;
    }

    private static CsvCandidate? ParseCsvCandidate(IReadOnlyList<string> record, IReadOnlyDictionary<string, int> headers, int lineNumber)
    {
        string Get(params string[] names)
        {
            foreach (var name in names)
            {
                if (!headers.TryGetValue(name, out var idx) || idx >= record.Count)
                    continue;

                var value = record[idx]?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        var rawType = Get("Type", "ItemType", "Category");
        var title = Get("Title", "Name", "Item", "Website", "Site");
        var url = Get("Url", "URL", "WebsiteUrl");
        var username = Get("Username", "Login", "User");
        var password = Get("Password", "Secret");
        var notes = Get("Notes", "Note");
        var cardholder = Get("Cardholder", "Card Holder");
        var number = Get("Number", "CardNumber");
        var expiryMonth = Get("ExpiryMonth", "ExpMonth");
        var expiryYear = Get("ExpiryYear", "ExpYear");
        var cvc = Get("Cvc", "CVV");
        var content = Get("Content", "Body", "Text");

        var type = ParseItemType(rawType, number, cvc, expiryMonth, expiryYear, cardholder, content);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var normalizedTitle = DetermineTitle(type, title, url, cardholder);

        string payloadJson;
        string duplicateKey;
        string secondaryText;

        switch (type)
        {
            case ItemType.Card:
            {
                var digits = new string(number.Where(char.IsDigit).ToArray());
                if (string.IsNullOrWhiteSpace(normalizedTitle) || digits.Length < 12)
                    return CsvCandidate.Invalid(lineNumber, type, normalizedTitle, "Card rows must include a title and card number.");

                var month = ParseInt(expiryMonth);
                var year = ParseInt(expiryYear);
                if (month is null || month is < 1 or > 12)
                    return CsvCandidate.Invalid(lineNumber, type, normalizedTitle, "Card expiry month must be between 1 and 12.");
                if (year is null || year is < 2000 or > 2100)
                    return CsvCandidate.Invalid(lineNumber, type, normalizedTitle, "Card expiry year must be between 2000 and 2100.");

                var cvcDigits = new string(cvc.Where(char.IsDigit).ToArray());
                if (cvcDigits.Length is < 3 or > 4)
                    return CsvCandidate.Invalid(lineNumber, type, normalizedTitle, "Card CVC must be 3 or 4 digits.");

                var payload = new CardPayload(normalizedTitle, cardholder, digits, month.Value, year.Value, cvcDigits, notes);
                payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                duplicateKey = string.Join("|", "card", NormalizeDuplicatePart(payload.Title), NormalizeDuplicatePart(payload.Cardholder), Last4(payload.Number));
                secondaryText = string.IsNullOrWhiteSpace(payload.Cardholder) ? Last4(payload.Number) : $"{payload.Cardholder} / {Last4(payload.Number)}";
                return new CsvCandidate(Guid.NewGuid().ToString("N"), lineNumber, type, normalizedTitle, secondaryText, payloadJson, duplicateKey, true, null, now, now);
            }
            case ItemType.Note:
            {
                if (string.IsNullOrWhiteSpace(normalizedTitle))
                    return CsvCandidate.Invalid(lineNumber, type, "Note", "Note rows must include a title.");

                var payload = new NotePayload(normalizedTitle, string.IsNullOrWhiteSpace(content) ? notes : content);
                payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                duplicateKey = string.Join("|", "note", NormalizeDuplicatePart(payload.Title));
                secondaryText = TrimSnippet(payload.Content);
                return new CsvCandidate(Guid.NewGuid().ToString("N"), lineNumber, type, normalizedTitle, secondaryText, payloadJson, duplicateKey, true, null, now, now);
            }
            case ItemType.Web:
            default:
            {
                if (string.IsNullOrWhiteSpace(normalizedTitle))
                    return CsvCandidate.Invalid(lineNumber, type, "Web", "Web login rows must include a title, url, or username.");

                var payload = new WebPayload(normalizedTitle, url, username, password, notes);
                payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                duplicateKey = string.Join("|", "web", NormalizeDuplicatePart(payload.Title), NormalizeDuplicatePart(payload.Username), NormalizeDuplicatePart(payload.Url));
                secondaryText = string.IsNullOrWhiteSpace(payload.Username)
                    ? payload.Url
                    : string.IsNullOrWhiteSpace(payload.Url)
                        ? payload.Username
                        : $"{payload.Username} / {payload.Url}";
                return new CsvCandidate(Guid.NewGuid().ToString("N"), lineNumber, type, normalizedTitle, secondaryText, payloadJson, duplicateKey, true, null, now, now);
            }
        }
    }

    private static ItemType ParseItemType(string rawType, string number, string cvc, string expiryMonth, string expiryYear, string cardholder, string content)
    {
        if (!string.IsNullOrWhiteSpace(rawType))
        {
            var normalized = rawType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "web" or "login" or "website" => ItemType.Web,
                "card" or "creditcard" or "credit card" => ItemType.Card,
                "note" or "markdown note" or "markdown-note" => ItemType.Note,
                _ => InferItemType(number, cvc, expiryMonth, expiryYear, cardholder, content)
            };
        }

        return InferItemType(number, cvc, expiryMonth, expiryYear, cardholder, content);
    }

    private static ItemType InferItemType(string number, string cvc, string expiryMonth, string expiryYear, string cardholder, string content)
    {
        if (!string.IsNullOrWhiteSpace(number) || !string.IsNullOrWhiteSpace(cvc) || !string.IsNullOrWhiteSpace(expiryMonth) || !string.IsNullOrWhiteSpace(expiryYear) || !string.IsNullOrWhiteSpace(cardholder))
            return ItemType.Card;

        if (!string.IsNullOrWhiteSpace(content))
            return ItemType.Note;

        return ItemType.Web;
    }

    private static string DetermineTitle(ItemType type, string title, string url, string cardholder)
    {
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();

        return type switch
        {
            ItemType.Card => !string.IsNullOrWhiteSpace(cardholder) ? cardholder.Trim() : "Card",
            ItemType.Note => "Markdown Note",
            _ => !string.IsNullOrWhiteSpace(url) ? url.Trim() : "Web Login"
        };
    }

    private static int? ParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string TrimSnippet(string text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var trimmed = text.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static List<List<string>> ParseCsvRecords(string csvText)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void Append(char value)
        {
            if (field.Length >= MaxCsvFieldChars)
                throw new InvalidDataException($"CSV field exceeds the {MaxCsvFieldChars} character limit.");

            field.Append(value);
        }

        void AddField()
        {
            if (row.Count >= MaxCsvColumns)
                throw new InvalidDataException($"CSV rows cannot exceed {MaxCsvColumns} columns.");

            row.Add(field.ToString());
            field.Clear();
        }

        void AddRow()
        {
            if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (records.Count >= MaxCsvRows + 1)
                    throw new InvalidDataException($"CSV import cannot exceed {MaxCsvRows} data rows.");

                records.Add(row.ToList());
            }

            row.Clear();
        }

        for (var i = 0; i < csvText.Length; i++)
        {
            var ch = csvText[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    AddField();
                    break;
                case '\r':
                    AddField();
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\n')
                        i++;
                    AddRow();
                    break;
                case '\n':
                    AddField();
                    AddRow();
                    break;
                default:
                    Append(ch);
                    break;
            }
        }

        if (inQuotes)
            throw new InvalidDataException("CSV contains an unterminated quoted field.");

        AddField();
        AddRow();

        return records;
    }

    private sealed record CsvCandidate(
        string Id,
        int LineNumber,
        ItemType Type,
        string Title,
        string SecondaryText,
        string PayloadJson,
        string DuplicateKey,
        bool IsValid,
        string? Error,
        string CreatedAtUtc,
        string UpdatedAtUtc)
    {
        public static CsvCandidate Invalid(int lineNumber, ItemType type, string title, string error)
            => new(Guid.NewGuid().ToString("N"), lineNumber, type, title, "", "", "", false, error, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.ToString("O"));

        public VaultCsvImportRowPreview ToPreview(VaultCsvRowStatus status, string? message)
            => new(LineNumber, Type, Title, SecondaryText, status, message);
    }

    private sealed record CsvImportAction(CsvCandidate Candidate, string? DeleteItemId);

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}

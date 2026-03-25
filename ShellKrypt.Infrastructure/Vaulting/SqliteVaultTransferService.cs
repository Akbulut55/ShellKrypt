using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => Summarize(await BuildSnapshotAsync(vaultPath, vaultKey, ct));

    public async Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
    {
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions), ct);
    }

    public async Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default)
    {
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var package = await CreateEncryptedPackageAsync(snapshotBytes, exportPassphrase, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(package, JsonOptions), ct);
    }

    public async Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default)
        => Summarize(await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct));

    public async Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var snapshot = await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct);
        await ImportSnapshotAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported snapshot version {snapshot.Version}.");

        var labelMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var existingItemIds = (await _repo.ListAsync(vaultPath, ct))
            .Select(x => x.Header.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var label in snapshot.Labels)
        {
            var stored = await _repo.UpsertLabelAsync(vaultPath, label.Name, label.Color, ct);
            labelMap[label.Id] = stored.Id;
        }

        foreach (var item in snapshot.Items)
        {
            if (existingItemIds.Contains(item.Id))
                await _repo.DeleteAsync(vaultPath, item.Id, ct);

            var header = new VaultItemHeader(item.Id, item.Type, item.Favorite, item.CreatedAtUtc, item.UpdatedAtUtc);
            var payload = Encoding.UTF8.GetBytes(item.PayloadJson);
            var encryptedPayload = AesGcmBlob.Encrypt(vaultKey, payload);
            await _repo.InsertAsync(vaultPath, header, encryptedPayload, ct);
            existingItemIds.Add(item.Id);
        }

        foreach (var item in snapshot.Items)
        {
            var labelIds = snapshot.ItemLabels
                .Where(x => x.ItemId == item.Id)
                .Select(x => labelMap.TryGetValue(x.LabelId, out var mappedId) ? mappedId : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();

            if (labelIds.Length > 0)
                await _repo.SetItemLabelsAsync(vaultPath, item.Id, labelIds, ct);
        }
    }

    public async Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
    {
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
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var duplicateKeyToId = BuildDuplicateKeyMap(snapshot);
        var csvText = await File.ReadAllTextAsync(csvPath, ct);
        var candidates = ParseCsvCandidates(csvText);

        var seenKeys = new HashSet<string>(duplicateKeyToId.Keys, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (!candidate.IsValid)
                continue;

            var duplicateDetected = seenKeys.Contains(candidate.DuplicateKey);
            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.SkipDuplicates)
                continue;

            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.OverwriteDuplicates && duplicateKeyToId.TryGetValue(candidate.DuplicateKey, out var existingId))
            {
                await _repo.DeleteAsync(vaultPath, existingId, ct);
                duplicateKeyToId.Remove(candidate.DuplicateKey);
                seenKeys.Remove(candidate.DuplicateKey);
            }

            var header = new VaultItemHeader(candidate.Id, candidate.Type, false, candidate.CreatedAtUtc, candidate.UpdatedAtUtc);
            var encryptedPayload = AesGcmBlob.Encrypt(vaultKey, Encoding.UTF8.GetBytes(candidate.PayloadJson));
            await _repo.InsertAsync(vaultPath, header, encryptedPayload, ct);
            duplicateKeyToId[candidate.DuplicateKey] = candidate.Id;
            seenKeys.Add(candidate.DuplicateKey);
        }
    }

    private readonly IItemRepository _repo = new SqliteItemRepository();

    private async Task<VaultSnapshot> BuildSnapshotAsync(string vaultPath, byte[] vaultKey, CancellationToken ct)
    {
        var rows = await _repo.ListAsync(vaultPath, ct);
        var labels = await _repo.ListLabelsAsync(vaultPath, ct);

        var items = new List<VaultSnapshotItem>(rows.Count);
        var itemLabels = new List<VaultSnapshotItemLabel>();

        foreach (var row in rows)
        {
            var payloadJson = Encoding.UTF8.GetString(AesGcmBlob.Decrypt(vaultKey, row.EncryptedPayload));
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

        var kdf = DefaultKdf();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derivedKey = await DeriveKeyAsync(passphrase, salt, kdf, ct);
        try
        {
            var encrypted = AesGcmBlob.Encrypt(derivedKey, plaintext);
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

    private static async Task<VaultSnapshot> ReadEncryptedSnapshotAsync(string packagePath, string passphrase, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(packagePath, ct);
        var package = JsonSerializer.Deserialize<VaultEncryptedPackage>(json, JsonOptions)
            ?? throw new InvalidOperationException("Encrypted export file is empty or invalid.");

        if (package.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported package version {package.Version}.");

        var salt = Convert.FromBase64String(package.SaltBase64);
        var encrypted = Convert.FromBase64String(package.CiphertextBase64);
        var derivedKey = await DeriveKeyAsync(passphrase, salt, package.Kdf, ct);
        try
        {
            var plaintext = AesGcmBlob.Decrypt(derivedKey, encrypted);
            return JsonSerializer.Deserialize<VaultSnapshot>(plaintext, JsonOptions)
                ?? throw new InvalidOperationException("Encrypted export payload is empty or invalid.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static VaultKdfParams DefaultKdf()
    {
        var p = Math.Max(1, Environment.ProcessorCount / 2);
        return new VaultKdfParams(65536, 3, p);
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
            _ => $"{(int)type}|{payloadJson.Trim()}"
        };
    }

    private static string BuildWebDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<WebPayload>(payloadJson, JsonOptions)
            ?? new WebPayload("", "", "", "", "", "", "");
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

    private static string BuildNoteDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<NotePayload>(payloadJson, JsonOptions)
            ?? new NotePayload("", "");
        return string.Join("|", "note", NormalizeDuplicatePart(payload.Title));
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
        var twoFaNote = Get("TwoFaNote", "TwoFactorNote", "2FA Note");
        var totpSecret = Get("TotpSecret", "TOTPSecret", "OtpSecret", "OTPSecret", "TOTP", "OTP", "2FA", "TwoFactor");
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

                var payload = new WebPayload(normalizedTitle, url, username, password, notes, twoFaNote, totpSecret);
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
                "note" or "secure note" or "secure-note" => ItemType.Note,
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
            ItemType.Note => "Note",
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
                    field.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    row.Add(field.ToString());
                    field.Clear();
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\n')
                        i++;
                    if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
                        records.Add(row.ToList());
                    row.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
                        records.Add(row.ToList());
                    row.Clear();
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        row.Add(field.ToString());
        if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
            records.Add(row.ToList());

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
}

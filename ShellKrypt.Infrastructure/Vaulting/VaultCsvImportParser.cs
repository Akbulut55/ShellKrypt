using System.Globalization;
using System.Text;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

internal static class VaultCsvImportParser
{
    private const int MaxCsvRows = 10000;
    private const int MaxCsvColumns = 64;
    private const int MaxCsvFieldChars = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };

    public static List<CsvCandidate> ParseCandidates(string csvText)
    {
        var records = ParseRecords(csvText);
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
            var candidate = ParseCandidate(records[rowIndex], index, rowIndex + 1);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        return candidates;
    }

    private static CsvCandidate? ParseCandidate(IReadOnlyList<string> record, IReadOnlyDictionary<string, int> headers, int lineNumber)
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
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                var duplicateKey = string.Join("|", "card", NormalizeDuplicatePart(payload.Title), NormalizeDuplicatePart(payload.Cardholder), Last4(payload.Number));
                var secondaryText = string.IsNullOrWhiteSpace(payload.Cardholder) ? Last4(payload.Number) : $"{payload.Cardholder} / {Last4(payload.Number)}";
                return new CsvCandidate(Guid.NewGuid().ToString("N"), lineNumber, type, normalizedTitle, secondaryText, payloadJson, duplicateKey, true, null, now, now);
            }
            case ItemType.Note:
            {
                if (string.IsNullOrWhiteSpace(normalizedTitle))
                    return CsvCandidate.Invalid(lineNumber, type, "Note", "Note rows must include a title.");

                var payload = new NotePayload(normalizedTitle, string.IsNullOrWhiteSpace(content) ? notes : content);
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                var duplicateKey = string.Join("|", "note", NormalizeDuplicatePart(payload.Title));
                var secondaryText = TrimSnippet(payload.Content);
                return new CsvCandidate(Guid.NewGuid().ToString("N"), lineNumber, type, normalizedTitle, secondaryText, payloadJson, duplicateKey, true, null, now, now);
            }
            case ItemType.Web:
            default:
            {
                if (string.IsNullOrWhiteSpace(normalizedTitle))
                    return CsvCandidate.Invalid(lineNumber, type, "Web", "Web login rows must include a title, url, or username.");

                var payload = new WebPayload(normalizedTitle, url, username, password, notes);
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                var duplicateKey = string.Join("|", "web", NormalizeDuplicatePart(payload.Title), NormalizeDuplicatePart(payload.Username), NormalizeDuplicatePart(payload.Url));
                var secondaryText = string.IsNullOrWhiteSpace(payload.Username)
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
        if (!string.IsNullOrWhiteSpace(number)
            || !string.IsNullOrWhiteSpace(cvc)
            || !string.IsNullOrWhiteSpace(expiryMonth)
            || !string.IsNullOrWhiteSpace(expiryYear)
            || !string.IsNullOrWhiteSpace(cardholder))
        {
            return ItemType.Card;
        }

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

    private static List<List<string>> ParseRecords(string csvText)
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

    private static string NormalizeDuplicatePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();

    private static string Last4(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return digits;

        return digits[^4..];
    }
}

internal sealed record CsvCandidate(
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

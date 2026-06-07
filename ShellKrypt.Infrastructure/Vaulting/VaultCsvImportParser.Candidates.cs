using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Vaulting;

internal static partial class VaultCsvImportParser
{
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
}

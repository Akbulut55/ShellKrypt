using System.Globalization;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Vaulting;

internal static partial class VaultCsvImportParser
{
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
}

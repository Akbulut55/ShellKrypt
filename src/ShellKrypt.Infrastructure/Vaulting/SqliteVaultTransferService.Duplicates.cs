using System.Text;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
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
            ItemType.QuickFillEntry => BuildQuickFillDuplicateKey(payloadJson),
            ItemType.ProjectSecret => BuildProjectSecretDuplicateKey(payloadJson),
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
            NormalizeDuplicatePart(payload.User),
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

    private static string BuildQuickFillDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<QuickFillEntryPayload>(payloadJson, JsonOptions)
            ?? new QuickFillEntryPayload("", "Other", true, new QuickFillTargetRule("", ""), Array.Empty<QuickFillField>(), false, "");

        return string.Join("|",
            "quick-fill",
            NormalizeDuplicatePart(payload.Name),
            NormalizeDuplicatePart(payload.Target.ProcessName));
    }

    private static string BuildProjectSecretDuplicateKey(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<ProjectSecretPayload>(payloadJson, JsonOptions)
            ?? new ProjectSecretPayload("", "", "", null, Array.Empty<ProjectSecretEnvironmentPayload>(), Array.Empty<ProjectSecretScanResult>());

        return string.Join("|",
            "project-secret",
            NormalizeDuplicatePart(payload.Name),
            NormalizeDuplicatePart(payload.ProjectRootPath));
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

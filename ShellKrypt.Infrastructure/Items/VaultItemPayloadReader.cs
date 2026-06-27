using System.Text.Json;
using ShellKrypt.Application.Items;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class VaultItemPayloadReader : IVaultItemPayloadReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WebPayload ReadWeb(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new WebPayload("", "", "", "", ""));

    public CardPayload ReadCard(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new CardPayload("", "", "", 0, 0, "", ""));

    public NotePayload ReadNote(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new NotePayload("", ""));

    public AuthenticatorPayload ReadAuthenticator(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new AuthenticatorPayload("", "", "", "", "", 6, 30, "", "", "", 0));

    public ApiKeyPayload ReadApiKey(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new ApiKeyPayload("", "", "", "", Array.Empty<ApiKeyFieldPayload>()));

    public ProjectSecretPayload ReadProjectSecret(VaultItemRow row, byte[] vaultKey)
        => ReadPayload(row, vaultKey, () => new ProjectSecretPayload("", "", "", null, Array.Empty<ProjectSecretEnvironmentPayload>(), Array.Empty<ProjectSecretLinkedApiKeyPayload>()));

    private static TPayload ReadPayload<TPayload>(VaultItemRow row, byte[] vaultKey, Func<TPayload> fallback)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload);
        return JsonSerializer.Deserialize<TPayload>(json, JsonOpts) ?? fallback();
    }
}

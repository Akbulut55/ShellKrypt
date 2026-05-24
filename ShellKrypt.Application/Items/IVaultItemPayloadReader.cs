using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public interface IVaultItemPayloadReader
{
    WebPayload ReadWeb(VaultItemRow row, byte[] vaultKey);
    CardPayload ReadCard(VaultItemRow row, byte[] vaultKey);
    NotePayload ReadNote(VaultItemRow row, byte[] vaultKey);
    AuthenticatorPayload ReadAuthenticator(VaultItemRow row, byte[] vaultKey);
    ApiKeyPayload ReadApiKey(VaultItemRow row, byte[] vaultKey);
}

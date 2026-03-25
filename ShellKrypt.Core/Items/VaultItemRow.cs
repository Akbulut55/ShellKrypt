namespace ShellKrypt.Core.Items;

public sealed record VaultItemRow(
    VaultItemHeader Header,
    byte[] EncryptedPayload,
    IReadOnlyList<VaultLabelRow> Labels
);

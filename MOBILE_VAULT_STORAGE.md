# Mobile Vault Storage

Default decision:

- Vaults are local app-private `.skvault` files.
- Import/export is user-initiated through the platform picker/share sheet.
- Encrypted backups use `.skbx`.
- Plaintext exports require stronger confirmation and a post-export warning.
- Cloud sync is not enabled by default.

This preserves ShellKrypt's local-only model while still allowing manual backup and transfer.

# Mobile Architecture

ShellKrypt mobile uses one shared shell for Android and iOS.

Shared:

- navigation structure
- welcome, unlock, vault, item list, detail, edit, settings, backup/export flows
- compact card lists instead of desktop tables
- full-screen pages instead of desktop modal dialogs
- theme keys from `ShellKrypt.UI.Shared`
- item search and section catalog from `ShellKrypt.UI.Shared`

Platform-specific:

- clipboard and best-effort clearing behavior
- secure storage implementation
- file picker and share sheet
- QR/image import
- privacy screen behavior
- background/foreground lifecycle events
- optional biometric unlock

Biometric unlock is convenience only. It must never become password recovery.

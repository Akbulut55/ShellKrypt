# ShellKrypt

Current version: `0.1.0-alpha`

ShellKrypt is a local-only encrypted vault desktop app built with .NET and Avalonia. It stores vault data on the user's device in `.skvault` files and provides dedicated screens for credentials, payment cards, API keys, authenticator codes, markdown notes, password generation, security audit, settings, and activity logs.

ShellKrypt is in active alpha development. Treat it as a private/testing build until the release checklist has been completed and the app has received broader validation.

## Critical Warning: No Password Recovery

ShellKrypt cannot recover a forgotten master password.

The master password is used to derive the key that unlocks the vault. If the vault is locked and the master password is lost, the encrypted data cannot be decrypted by ShellKrypt, the developer, or anyone else without a valid backup and its backup passphrase.

Before relying on a vault, create and verify a backup. If the vault is still unlocked and you suspect you may lose access, change the master password or export a backup before locking the vault.

## Features

- Local encrypted vault files with create, import, open, edit, default-vault, and delete flows.
- All Items dashboard for viewing vault records across supported item types.
- Web Logins with username, email, password, URL, notes, copy, edit, delete, search, filter, and pagination.
- Credit Cards with bank, cardholder, issuer, card type, masked number, CVC reveal, expiry checks, copy, edit, delete, search, filter, and pagination.
- API Keys with flexible metadata fields for providers that use IDs, prefixes, project names, project numbers, key names, secrets, or other custom fields.
- Authenticator for local TOTP/HOTP codes with manual secret entry, QR screenshot import, image paste support, and advanced code options.
- Markdown Notes with source/preview switching, starred notes, search, create, edit, and delete.
- Password Generator with local password creation, strength indicator, configurable length, and character class controls.
- Cryptographic Workbench with SHA-256, SHA-512, and Base64 encode/decode utilities.
- Security Audit for weak, reused, and stale credentials with remediation routing.
- Settings for auto-lock, lock on focus loss, clipboard clearing, language, theme, backup/restore, CSV import, and master-password changes.
- Per-vault encrypted Activity Logs with filtering, pagination, metadata details, log clearing, and JSON report export.

## Security Model

ShellKrypt is designed for local storage only. There is no cloud sync or remote account recovery layer.

- Vaults are stored as local `.skvault` SQLite databases.
- Sensitive item payloads are encrypted before being written to the vault database.
- The vault key is protected by a key derived from the master password using Argon2id.
- Encrypted item payloads use AES-GCM.
- The vault key remains available in app memory while the vault is unlocked.
- Activity logs are stored inside the active vault database instead of a global app-level log file.
- Clipboard copy actions can be cleared automatically after a configured timeout.
- JSON exports are intentionally decrypted reports. Store them carefully and delete them when no longer needed.
- The project has not received an external security audit.

## Project Structure

```text
ShellKrypt.Core/
  Domain models, payload records, service interfaces, security settings, and transfer models.

ShellKrypt.Infrastructure/
  SQLite vault storage, encrypted payload persistence, Argon2-based unlock, backup/restore, import/export, and activity log persistence.

ShellKrypt.Desktop/
  Avalonia desktop app, views, viewmodels, UI services, assets, and platform integration.

ShellKrypt.Tests/
  xUnit tests for core and infrastructure behavior.
```

Dependency direction:

```text
ShellKrypt.Desktop -> ShellKrypt.Core
ShellKrypt.Desktop -> ShellKrypt.Infrastructure
ShellKrypt.Infrastructure -> ShellKrypt.Core
ShellKrypt.Tests -> ShellKrypt.Core
ShellKrypt.Tests -> ShellKrypt.Infrastructure
```

## Requirements

- .NET 10 SDK
- Windows is the primary tested desktop target
- Avalonia can support additional desktop platforms, but macOS/Linux release behavior should be validated separately

## Run Locally

```powershell
dotnet restore .\ShellKrypt.slnx
dotnet run --project .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj
```

## Build

```powershell
dotnet build .\ShellKrypt.slnx
```

To keep generated output isolated:

```powershell
dotnet build .\ShellKrypt.slnx --artifacts-path .\artifacts
```

## Test

```powershell
dotnet test .\ShellKrypt.slnx
```

## Release Instructions

1. Update the version in `ShellKrypt.Desktop/ShellKrypt.Desktop.csproj` and this README.
2. Confirm the working tree only contains intentional release changes.
3. Run restore, build, and tests.
4. Create a clean publish output.
5. Smoke-test the published executable with a new vault and an existing vault.
6. Verify backup/restore, lock/unlock, wrong-password handling, item CRUD, authenticator codes, activity log export, and settings.
7. Package the publish output and attach it to the release.

Windows self-contained single-file publish:

```powershell
dotnet publish .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

Framework-dependent publish:

```powershell
dotnet publish .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64-framework-dependent
```

The Windows executable is produced as:

```text
publish\win-x64\ShellKrypt.Desktop.exe
```

Do not commit generated release output such as `publish/`, `artifacts*/`, `bin/`, or `obj/`.

## Release Checklist

- `dotnet build .\ShellKrypt.slnx` passes.
- `dotnet test .\ShellKrypt.slnx` passes.
- `dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive` reports no vulnerable packages.
- New vault creation works.
- Existing vault import/open works.
- Wrong master password is rejected.
- Master password change works while unlocked.
- Web Logins, Credit Cards, API Keys, Authenticator, and Markdown Notes support create, view, edit, delete, copy/reveal where applicable.
- Security Audit updates after credential changes.
- Activity Logs are scoped to the active vault.
- Activity report exports as JSON.
- Auto-lock, lock on focus loss, and clipboard timeout behave as expected.
- Plaintext export warning, clipboard clearing, and vault deletion confirmation are smoke-tested in the published build.
- Backup export and restore are tested.
- The no-password-recovery warning is visible in release notes or user-facing documentation.

## Development Notes

- Avalonia views are resolved through `ViewLocator`, which maps viewmodels to views by naming convention.
- Keep UI logic in desktop viewmodels/services and vault/domain behavior in Core or Infrastructure.
- Build output folders and generated artifacts should remain uncommitted.
- Before changing vault schema or encryption behavior, add or update tests in `ShellKrypt.Tests`.

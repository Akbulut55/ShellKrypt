# ShellKrypt

ShellKrypt is a local-only encrypted desktop vault for people who want to keep sensitive records on their own device instead of syncing them through a cloud account. It is built with .NET 10 and Avalonia, stores vaults as local `.skvault` SQLite databases, and provides separate workspaces for credentials, payment cards, API keys, authenticator codes, markdown notes, password generation, security review, settings, and activity logs.

ShellKrypt is currently a pre-1.0 private release build. It is intended for careful local use and broader validation before a public 1.0 release.

## What ShellKrypt Stores

- Web logins with usernames, emails, passwords, URLs, notes, copy actions, search, filtering, details, edit, delete, and pagination.
- Credit cards with bank, issuer, cardholder, card type, masked number, CVC reveal, expiry handling, copy actions, edit, delete, search, filtering, and pagination.
- API keys with flexible metadata fields for provider IDs, project IDs, project names, key names, prefixes, secrets, and custom fields.
- Authenticator entries for local TOTP/HOTP codes with manual secret entry, QR screenshot import, pasted image import, advanced code options, details, edit, and delete.
- Markdown notes with source/preview switching, autosave after typing stops, starred notes, search, create, edit, and delete.
- Activity logs stored inside the active vault with category filters, pagination, metadata details, clearing, and plaintext JSON report export.

## Core Workflows

- Create, import, open, delete, rename, and set a default local vault.
- Unlock a vault with a master password derived through Argon2id.
- Add and manage web logins, cards, API keys, authenticators, and markdown notes from dedicated screens.
- View all supported records from the All Items dashboard with search, filters, pagination, and cross-item overview.
- Generate local passwords with configurable length and character classes.
- Use the crypto workbench for SHA-256, SHA-512, and Base64 encode/decode utilities.
- Run a security audit for weak, reused, and stale web login passwords with remediation routing.
- Configure auto-lock, lock on focus loss, clipboard clearing, copy permissions, theme, backup/restore, CSV import, and master password changes.

## Security And Privacy Model

ShellKrypt is designed for local storage only. There is no ShellKrypt cloud account, no cloud sync layer, and no remote account recovery service.

- Vaults are stored as local `.skvault` SQLite databases.
- Sensitive item payloads are encrypted before being written to the vault database.
- The vault key is protected by a key derived from the master password using Argon2id.
- Encrypted item payloads use AES-GCM with versioned blob envelopes.
- Encrypted payloads are bound to practical associated data such as item type and item id.
- Activity logs are encrypted and stored inside the active vault database.
- Clipboard copy actions can be disabled or cleared automatically after a configured timeout, but clipboard clearing is best-effort and is not a security boundary.
- The vault key and visible secrets can exist in app memory while the vault is unlocked.
- JSON exports and activity report exports are intentionally plaintext reports. Store them carefully and delete them when no longer needed.
- The project has not received an external security audit.

## Critical Warning: No Password Recovery

ShellKrypt cannot recover a forgotten master password.

The master password is used to derive the key that unlocks the vault. If the vault is locked and the master password is lost, the encrypted data cannot be decrypted by ShellKrypt, the developer, or anyone else without a valid backup and its backup passphrase.

Before relying on a vault, create and verify a backup. If the vault is still unlocked and you suspect you may lose access, change the master password or export a backup before locking the vault.

## Current Limitations

- Windows is the primary tested desktop target.
- The interface is currently English-first. Additional languages should be added before a broad public 1.0 release.
- macOS and Linux behavior should be validated separately before publishing builds for those platforms.
- Code signing, installer packaging, update delivery, commercial legal text, and public support processes should be finalized before broad commercial distribution.

## Project Structure

```text
ShellKrypt.Core/
  Domain models, payload records, service interfaces, security settings, and transfer models.

ShellKrypt.Infrastructure/
  SQLite vault storage, encrypted payload persistence, Argon2-based unlock, backup/restore, import/export, and activity log persistence.

ShellKrypt.Application/
  Shared use-cases, session/state helpers, registry/settings services, item summaries, filters, and pagination logic.

ShellKrypt.UI.Shared/
  Shared theme resources, reusable UI controls, converters, and cross-shell visual primitives.

ShellKrypt.Desktop/
  Avalonia desktop app, views, viewmodels, UI services, assets, and platform integration.

ShellKrypt.Mobile/
  Shared Avalonia mobile UI that can be hosted by Android and iOS platform heads.

ShellKrypt.Mobile.Android/
  Android app head and Android-specific package metadata.

ShellKrypt.Mobile.iOS/
  iOS app head and iOS-specific package metadata.

ShellKrypt.Tests/
  xUnit tests for core and infrastructure behavior.
```

Dependency direction:

```text
ShellKrypt.Application -> ShellKrypt.Core
ShellKrypt.Infrastructure -> ShellKrypt.Core
ShellKrypt.Desktop -> ShellKrypt.Core/Application/Infrastructure/UI.Shared
ShellKrypt.Mobile -> ShellKrypt.Core/Application/Infrastructure/UI.Shared
ShellKrypt.Mobile.Android -> ShellKrypt.Mobile
ShellKrypt.Mobile.iOS -> ShellKrypt.Mobile
ShellKrypt.Tests -> ShellKrypt.Core
ShellKrypt.Tests -> ShellKrypt.Application
ShellKrypt.Tests -> ShellKrypt.Infrastructure
```

## Solution Layout

`ShellKrypt.slnx` is the canonical root solution. It includes the workload-neutral projects used for normal desktop development, shared mobile UI development, and tests.

Android and iOS platform heads are built directly from their project files instead of through a second root solution. This keeps the default solution build usable on Windows without requiring optional mobile workloads or iOS build tooling.

## Requirements

- .NET 10 SDK
- Windows as the primary tested desktop target

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

Android app head build:

```powershell
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

iOS app head build requires the iOS workload and supported Apple build environment:

```powershell
dotnet build .\ShellKrypt.Mobile.iOS\ShellKrypt.Mobile.iOS.csproj -f net10.0-ios
```

## Test

```powershell
dotnet test .\ShellKrypt.slnx
dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive
```

## Publish A Windows Build

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

## Pre-Release Smoke Test

- Build, tests, and dependency vulnerability check pass.
- New vault creation works.
- Existing vault import/open works.
- Wrong master password is rejected.
- Master password change works while unlocked.
- Web Logins, Credit Cards, API Keys, Authenticator, and Markdown Notes support create, view, edit, delete, copy/reveal where applicable.
- Markdown Notes autosave waits until typing stops before saving.
- Security Audit updates after credential changes.
- Activity Logs are scoped to the active vault.
- Activity report export is clearly presented as plaintext JSON.
- Auto-lock, lock on focus loss, clipboard timeout, and copy-disabled mode behave as expected.
- Plaintext export warning, clipboard clearing, and vault deletion confirmation are smoke-tested in the published build.
- Backup export and restore are tested.
- The no-password-recovery warning is visible in user-facing documentation.

## Development Notes

- Avalonia views are resolved through `ViewLocator`, which maps viewmodels to views by naming convention.
- Keep UI logic in desktop viewmodels/services and vault/domain behavior in Core or Infrastructure.
- Build output folders and generated artifacts should remain uncommitted.
- Before changing vault schema or encryption behavior, add or update tests in `ShellKrypt.Tests`.

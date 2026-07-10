# ShellKrypt

ShellKrypt is a local-only encrypted desktop vault for people who want to keep sensitive records on their own device instead of syncing them through a cloud account. It is built with .NET 10 and Avalonia, stores vaults as local `.skvault` SQLite databases, and provides workspaces for web logins, credit cards, API keys, authenticator codes, markdown notes, password generation, security review, Backup Center, settings, and activity logs.

ShellKrypt is currently a pre-1.0 desktop product. The source repository is prepared for public visibility, while official signed builds and paid distribution channels may be provided separately.

## Status

- Stage: pre-1.0 desktop build
- Current app version: `0.17.10`
- Primary surface: Windows desktop
- Secondary surfaces: shared mobile shell with Android and iOS app heads
- Owner: independent project owner
- Security status: not externally audited
- License: GPL-3.0-or-later

## What ShellKrypt Stores

- Web logins with title, username, email, password, URL, notes, copy actions, search, filters, details, edit, delete, and pagination.
- Credit cards with bank, issuer, cardholder, card type, masked number, CVC reveal, expiry handling, copy actions, details, edit, delete, search, filters, and pagination.
- API keys with provider, environment, notes, and encrypted flexible fields for API keys, tokens, client IDs, project IDs, prefixes, secrets, and custom values.
- Authenticator entries for local TOTP/HOTP codes with manual secret entry, QR screenshot import, pasted image import, advanced code options, details, edit, and delete.
- Markdown notes with source/preview switching, autosave after typing stops, starred notes, search, create, edit, and delete.
- Backup Center history and automatic-backup schedule state stored as local app metadata. Backup passphrases are not stored.
- Activity logs stored inside the active vault with category filters, pagination, metadata details, clearing, and plaintext JSON report export.

## Core Workflows

- Create, import, open, delete, rename, and set a default local vault.
- Unlock a vault with a master password derived through Argon2id.
- Add and manage web logins, cards, API keys, authenticators, and markdown notes from dedicated screens.
- View all supported records from the All Items dashboard with search, filters, pagination, and cross-item overview.
- Generate local passwords with configurable length and character classes from Password Generator.
- Use the crypto workbench for SHA-256, SHA-512, and Base64 encode/decode utilities.
- Run a local Security Audit for password, card, API key, and settings posture findings with remediation routing.
- Create, verify, restore, and track encrypted `.skbx` backups from Backup Center.
- Export plaintext JSON reports only after explicit confirmation.
- Preview and import CSV data through Backup Center.
- Review backup health and automatic-backup readiness from Backup Center.
- Configure auto-lock, lock on focus loss, clipboard clearing, copy permissions, theme, language, and master password changes from Settings.

## Security And Privacy Model

ShellKrypt is designed for local storage only. There is no ShellKrypt cloud account, no cloud sync layer, and no remote account recovery service.

- Vaults are stored as local `.skvault` SQLite databases.
- Sensitive item payloads are encrypted before being written to the vault database.
- The vault key is protected by a key derived from the master password using Argon2id.
- Encrypted item payloads use AES-GCM with versioned blob envelopes.
- Encrypted payloads are bound to practical associated data such as item type and item id.
- Activity logs are encrypted and stored inside the active vault database.
- Backup Center creates encrypted `.skbx` packages with a separate backup passphrase.
- In-app automatic backups run only while ShellKrypt is open, the vault is unlocked, and the user has entered the backup passphrase for the current session.
- Backup Center stores local backup history and automatic-backup schedule state. It does not store backup passphrases, hints, or recovery secrets.
- Clipboard copy actions can be disabled or cleared automatically after a configured timeout, but clipboard clearing is best-effort and is not a security boundary.
- The vault key and visible secrets can exist in app memory while the vault is unlocked.
- JSON exports and activity report exports are intentionally plaintext reports. Store them carefully and delete them when no longer needed.
- The desktop launcher requires a first-use security acknowledgement before creating, importing, or opening a vault.
- The project has not received an external security audit.

## Critical Warning: No Password Recovery

ShellKrypt cannot recover a forgotten master password.

The master password is used to derive the key that unlocks the vault. If the vault is locked and the master password is lost, the encrypted data cannot be decrypted by ShellKrypt, the developer, or anyone else without a valid backup and its backup passphrase.

Before relying on a vault, create and verify a backup. If the vault is still unlocked and you suspect you may lose access, change the master password or export a backup before locking the vault.

## Current Limitations

- Windows is the primary tested desktop target.
- English and Turkish runtime localization are available.
- Quick Fill is unfinished/experimental and should not be treated as a release-ready auto-fill feature yet.
- macOS and Linux behavior should be validated separately before publishing desktop builds for those platforms.
- Mobile app heads exist, but the mobile product is not feature-complete.
- Code signing, installer packaging, update delivery, public support processes, and export-compliance review should be finalized before broad commercial distribution.

## Project Documents

Public project documents:

- `SECURITY.md` - auth, data, secrets, privacy, and threat model.
- `DISCLAIMER.md` - no-warranty, no-recovery, export, clipboard, and audit disclaimers.
- `TERMS.md` - ShellKrypt terms of use.
- `PRIVACY.md` - local-only privacy notice.
- `LICENSE` - source license terms.
- `NOTICE.md` - official-build, modified-build, and branding notice.
- `CHANGELOG.md` - project-level change history.

Internal product plans, engineering notes, and agent instructions are intentionally kept outside the public documentation set.

## Repository Layout

```text
ShellKrypt/
|-- src/
|   |-- ShellKrypt.Core/
|   |-- ShellKrypt.Application/
|   |-- ShellKrypt.Infrastructure/
|   |-- ShellKrypt.UI.Shared/
|   |-- ShellKrypt.Desktop/
|   |-- ShellKrypt.Mobile/
|   |-- ShellKrypt.Mobile.Android/
|   `-- ShellKrypt.Mobile.iOS/
|-- tests/
|   `-- ShellKrypt.Tests/
|-- docs/
|   `-- README.md
|-- ShellKrypt.slnx
|-- README.md
|-- SECURITY.md
|-- DISCLAIMER.md
|-- TERMS.md
|-- PRIVACY.md
|-- LICENSE
|-- NOTICE.md
|-- CHANGELOG.md
```

Project responsibilities:

- `src/ShellKrypt.Core` contains domain models, payload records, service interfaces, security settings, and transfer models.
- `src/ShellKrypt.Application` contains shared use-cases, session/state helpers, registry/settings services, item summaries, filters, and pagination logic.
- `src/ShellKrypt.Infrastructure` contains SQLite vault storage, encrypted payload persistence, Argon2-based unlock, backup/restore, import/export, file stores, path guards, and activity log persistence.
- `src/ShellKrypt.UI.Shared` contains shared theme resources, reusable UI controls, converters, and cross-shell visual primitives.
- `src/ShellKrypt.Desktop` contains the Avalonia desktop app, views, viewmodels, UI services, assets, and desktop platform integration.
- `src/ShellKrypt.Mobile` contains the shared Avalonia mobile UI and mobile viewmodels.
- `src/ShellKrypt.Mobile.Android` and `src/ShellKrypt.Mobile.iOS` contain platform app heads and package metadata.
- `tests/ShellKrypt.Tests` contains xUnit tests for core, application, infrastructure, desktop adapter, and mobile shared behavior.

Dependency direction:

```text
ShellKrypt.Application -> ShellKrypt.Core
ShellKrypt.Infrastructure -> ShellKrypt.Core/Application
ShellKrypt.Desktop -> ShellKrypt.Core/Application/Infrastructure/UI.Shared
ShellKrypt.Mobile -> ShellKrypt.Core/Application/Infrastructure/UI.Shared
ShellKrypt.Mobile.Android -> ShellKrypt.Mobile
ShellKrypt.Mobile.iOS -> ShellKrypt.Mobile
ShellKrypt.Tests -> ShellKrypt.Core/Application/Infrastructure/Desktop/Mobile/UI.Shared
```

## Solution Layout

`ShellKrypt.slnx` is the canonical root solution. It includes workload-neutral projects used for normal desktop development, shared mobile UI development, and tests.

Android and iOS platform heads are built directly from their project files instead of through a second root solution. This keeps the default solution build usable on Windows without requiring optional mobile workloads or iOS build tooling.

## Requirements

- .NET 10 SDK
- Windows for the primary tested desktop workflow
- Android workload, Android SDK, and an emulator/device for Android builds
- macOS, Xcode, Apple signing/provisioning, and the .NET iOS workload for iOS builds

## Run Locally

```powershell
dotnet restore .\ShellKrypt.slnx
dotnet run --project .\src\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj
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
dotnet build .\src\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

iOS app head build requires the iOS workload and supported Apple build environment:

```powershell
dotnet build .\src\ShellKrypt.Mobile.iOS\ShellKrypt.Mobile.iOS.csproj -f net10.0-ios
```

## Test

```powershell
dotnet test .\ShellKrypt.slnx
dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive
```

## Publish A Windows Build

Windows self-contained single-file publish:

```powershell
dotnet publish .\src\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

Framework-dependent publish:

```powershell
dotnet publish .\src\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64-framework-dependent
```

The Windows executable is produced as:

```text
publish\win-x64\ShellKrypt.exe
```

Do not commit generated release output such as `publish/`, `artifacts*/`, `bin/`, or `obj/`.

## First Useful Workflow

```text
User creates a vault
  -> chooses a master password
  -> ShellKrypt derives an unlock key and initializes a local .skvault database
  -> user adds an encrypted web login, card, API key, authenticator, or markdown note
  -> user locks and later unlocks the vault with the same master password
```

Acceptance:

- A new vault can be created, unlocked, locked, reopened, and deleted.
- Sensitive item payloads are encrypted in the vault database.
- Forgetting the master password does not expose a recovery path.

## Pre-Release Smoke Test

- Build, tests, and dependency vulnerability check pass.
- New vault creation works.
- Existing vault import/open works.
- Unlock and lock flows work.
- All item types can be added, viewed, edited, deleted, searched, and paged.
- Backup Center can create, verify, restore, and track encrypted backups with a separate passphrase.
- Automatic backups run only while the app is open, the vault is unlocked, and a session-only backup passphrase is available.
- Backup Center backup-health and automatic-backup status work without storing recovery secrets.
- Plaintext export requires explicit confirmation and produces a warning.
- Clipboard copy, clearing, and disabled-copy settings work as documented.
- Activity logs load, filter, export, and clear without recording raw secrets.
- Vault deletion confirms the selected `.skvault` and does not delete unexpected paths.

## Contributing Notes

- Keep product direction aligned with the current README, security model, and user-facing legal/privacy documents.
- Keep implementation work scoped, testable, and consistent with the project boundaries described above.
- Update `CHANGELOG.md` for meaningful changes.
- Do not commit secrets, real user data, private logs, generated outputs, local vaults, local backups, plaintext exports, or local environment files.

## License

ShellKrypt source code is prepared for release under `GPL-3.0-or-later`. See `LICENSE` for the full GPL v3 text.

Official signed builds, paid distribution channels, support services, names, logos, and release infrastructure may be provided separately from the source license. See `NOTICE.md` for the official-build and modified-build notice.

## Disclaimer

ShellKrypt is provided as-is and has not received an external security audit. There is no password recovery. Clipboard clearing is best-effort. Plaintext exports are decrypted reports and must be handled carefully.

See `TERMS.md`, `PRIVACY.md`, `DISCLAIMER.md`, and `SECURITY.md` before publishing or distributing the project.

# ShellKrypt

ShellKrypt is a local-only encrypted vault desktop app built with .NET and Avalonia. It stores sensitive vault items on the user's device, including web logins, secure notes, credit cards, and generated passwords, with supporting tools for password generation, hashing, Base64 encoding/decoding, import/export, and password health checks.

The app is currently in active desktop UI development, with several screens being aligned to Stitch-generated designs.

## Features

- Encrypted local vault files (`.skvault`)
- Web login storage with username, email, password, URL, and notes
- Secure notes
- Credit card storage with card details, issuer/type metadata, reveal/copy actions, pagination, and expiry summaries
- Password generator with configurable length and character classes
- Cryptographic workbench with SHA-256, SHA-512, and Base64 tools
- Password health audit for reused and weak passwords
- Encrypted backup export/import (`.skbx`)
- Plaintext JSON export with explicit confirmation
- CSV import preview and duplicate handling
- Vault registry, default vaults, vault import, vault duplication, and vault deletion confirmation
- Auto-lock, lock-on-deactivate, theme, and clipboard-clear settings

## Project Structure

```text
ShellKrypt.Core/
  Shared domain models, payload records, interfaces, and vault transfer models.

ShellKrypt.Infrastructure/
  SQLite vault/item storage, AES-GCM payload encryption helpers, Argon2-based vault unlock, and import/export implementations.

ShellKrypt.Desktop/
  Avalonia desktop app, views, viewmodels, services, assets, and UI resources.

ShellKrypt.Tests/
  xUnit tests for core/infrastructure behavior such as vault transfer workflows.
```

The dependency direction is:

```text
ShellKrypt.Desktop -> ShellKrypt.Core
ShellKrypt.Desktop -> ShellKrypt.Infrastructure
ShellKrypt.Infrastructure -> ShellKrypt.Core
ShellKrypt.Tests -> ShellKrypt.Core
ShellKrypt.Tests -> ShellKrypt.Infrastructure
```

## Requirements

- .NET 10 SDK
- Windows, macOS, or Linux supported by Avalonia Desktop

The app currently targets `net10.0`.

## Build

```powershell
dotnet build ShellKrypt.slnx
```

To keep generated build output in a separate folder:

```powershell
dotnet build ShellKrypt.slnx --artifacts-path artifacts
```

## Run

```powershell
dotnet run --project ShellKrypt.Desktop
```

## Test

```powershell
dotnet test ShellKrypt.slnx
```

## Security Model

ShellKrypt is designed to always keep vault data local. Vault files are stored on disk as SQLite databases, while item payloads are encrypted before being written. The master password is used to derive an unlock key with Argon2id, and vault/item payload encryption uses AES-GCM.

Important notes:

- Unlocking a vault keeps the vault key available in app memory until the vault is locked.
- Clipboard copy actions can be automatically cleared after a configured delay.
- Plaintext JSON export intentionally writes decrypted vault content and should be handled with care.
- This project has not been described as externally audited, so treat it as an active development project rather than a formally reviewed security product.

## Current UI Areas

- `Vault`: planned to become the all-items dashboard.
- `Web Logins`: active credential list and login detail modal.
- `Secure Notes`: encrypted notes area.
- `Credit Cards`: active payment card list and card detail modal.
- `Security Audit`: password reuse/weakness overview.
- `Generator`: password generator and cryptographic utility workbench.
- `Settings`: import/export, lock behavior, theme, clipboard, and vault status settings.
- `Activity`: placeholder for a future vault activity timeline.

## Development Notes

- Avalonia views are resolved through `ViewLocator`, which maps viewmodels to views by naming convention.
- Build output folders such as `bin/`, `obj/`, and local `artifacts*/` directories should remain uncommitted.
- The app is currently being refactored screen-by-screen, so UI structure may change as Stitch-aligned views are implemented.

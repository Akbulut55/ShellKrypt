# ShellKrypt

ShellKrypt is a local-only encrypted vault for individuals who want to manage credentials, project secrets, authenticator codes, cards, and notes in user-controlled files without a cloud account. Its first useful workflow is creating a local vault and storing an encrypted record.

## Status

- Stage: active pre-1.0 development.
- Status note: Desktop workflows are functional, but packaging and public-release validation are incomplete.
- Main audience: Developers and privacy-conscious individuals who prefer local encrypted storage.
- Maintainer: Independent project owner.

## Highlights

- Stores sensitive item payloads and vault-scoped activity details encrypted in local `.skvault` files.
- Provides dedicated workspaces for Web Logins, Credit Cards, API Keys, Project Secrets, Authenticator, Markdown Notes, Security Audit, backups, and Crypto Tools.
- Requires no ShellKrypt cloud account, hosted synchronization service, telemetry service, or remote recovery provider.

## Requirements

- .NET 10 SDK.
- Windows or Linux desktop environment supported by Avalonia for current desktop development.

## Quick Start

```bash
dotnet restore ShellKrypt.slnx
dotnet run --project src/ShellKrypt.Desktop/ShellKrypt.Desktop.csproj
```

Full setup, build, test, and platform commands are documented in the private development guide used by maintainers.

## Example

```text
Create and reopen an encrypted vault
Input or action: Create a local vault, choose a master password, and add a web login.
Result: ShellKrypt encrypts the item in the .skvault file and restores it after a successful lock and unlock cycle.
```

## Limitations

- ShellKrypt is pre-1.0, has not received an external security audit, and should not be treated as a certified regulated-data platform.
- Code signing, installers, update delivery, and public support processes are not release-ready.

## Security Or Privacy Notes

- There is intentionally no password recovery. A forgotten master password or backup passphrase can make encrypted data permanently inaccessible.
- Plaintext exports and clipboard values leave the encrypted vault boundary; clipboard clearing is best-effort.
- ShellKrypt does not collect vault data through a ShellKrypt backend by default. Read [`SECURITY.md`](SECURITY.md), [`PRIVACY.md`](PRIVACY.md), and [`DISCLAIMER.md`](DISCLAIMER.md) before relying on the project.

## License

Copyright (C) 2026 the ShellKrypt author, publishing as Karvulas.

ShellKrypt is source-available commercial software under the
[`ShellKrypt Source License 1.0`](LICENSE). The source may be inspected,
compiled, and modified for personal noncommercial use, but redistribution and
commercial use require separate permission from the copyright owner.

ShellKrypt is not open-source software as defined by the Open Source
Initiative.

## Notes

- The current desktop application version is `0.26.0`.
- The official source repository is [Akbulut55/ShellKrypt](https://github.com/Akbulut55/ShellKrypt).
- `ShellKrypt.slnx` is the canonical solution for the Desktop application and shared libraries.
- Official signed builds, distribution channels, support, names, and branding may be governed separately as described in [`NOTICE.md`](NOTICE.md).

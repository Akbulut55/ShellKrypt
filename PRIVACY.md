# ShellKrypt: Privacy Notice

Status: draft for locally distributed pre-release desktop builds.

This notice explains what ShellKrypt stores, processes, shares, and avoids
collecting. Delete the open questions and review the notice before public
release.

## Summary

> ShellKrypt does not collect personal data through a ShellKrypt backend. It stores and processes user-provided vault data locally for encrypted-vault workflows and does not provide hosted accounts, telemetry, cloud sync, or remote recovery by default.

## Scope

This notice applies to:

- ShellKrypt desktop builds and their local vault, settings, backup, export, clipboard, and activity workflows.
- Shared mobile foundations when they use the same local-only storage model.
- Local files created explicitly through ShellKrypt.

This notice does not apply to:

- Third-party operating systems, app stores, package sources, clipboard managers, backup tools, cloud-sync clients, or distribution channels.
- Files users independently upload, share, copy, or process outside ShellKrypt.

## Data The Project Handles

| Data Category | Purpose | Required | Notes |
|---|---|---:|---|
| Vault records and activity | Store credentials, notes, project records, and local history | Yes for vault use | Sensitive payloads and activity details are encrypted in `.skvault` files |
| App settings and vault registry | Remember preferences and known local vault paths | No | Stored in local app metadata; does not contain raw item secrets |
| Backups and exports | User-directed backup, restore, and reporting | No | `.skbx` is encrypted; plaintext exports are decrypted files |
| Clipboard contents | User-directed copy actions | No | Shared with the operating-system clipboard temporarily and cleared best-effort |

## Data The Project Does Not Collect

- Master passwords, backup passphrases, vault contents, or exports through a ShellKrypt server.
- Usage analytics, advertising identifiers, or telemetry by default.
- Crash reports, hosted account identifiers, or remote recovery material by default.

## How Data Is Used

The project uses data to:

- Unlock, display, edit, search, audit, back up, restore, import, and export local vault records.
- Remember local settings, vault locations, safe operation history, and audit dismissals.
- Fulfill explicit copy, reveal, file-picker, scanner, backup, and export actions.

The project does not use data to:

- Build advertising profiles, sell user data, or operate analytics.
- Synchronize or recover vaults through a ShellKrypt-hosted service.

## External Sharing

| External System Or Action | Data Shared | Reason | Optional |
|---|---|---|---:|
| Operating-system clipboard | Value explicitly copied by the user | Paste into another application | Yes |
| User-selected filesystem location | Vault, backup, export, or report selected by the user | Local storage or transfer | Yes |
| User-selected project scan folder | Local filenames and text read by the scanner | Explicit Project Secrets analysis | Yes |

ShellKrypt does not automatically send this data to the project owner. Other software on the device may observe files, paths, clipboard contents, or process memory according to operating-system permissions.

## Choices, Deletion, And Export

- Choices or controls: users choose vault locations, clipboard behavior, backup settings, imports, exports, scans, and optional app preferences.
- Delete data: delete items or a confirmed vault through the app, and separately delete backups, exports, reports, and app metadata from their locations.
- Export data: create an encrypted `.skbx` backup or an explicitly warned plaintext report.
- Close account or remove access: not applicable because ShellKrypt has no hosted account by default.

## Security And Limits

- There is no password recovery; a forgotten master password or backup passphrase may make data permanently inaccessible.
- Plaintext exports, clipboard values, unlocked process memory, local paths, and user-shared files are outside the encrypted-at-rest vault boundary.
- ShellKrypt is not externally audited, and local encryption does not protect a fully compromised device or malicious build.

## Changes

This notice may change when ShellKrypt changes how it handles data. Meaningful privacy changes should be recorded in [`CHANGELOG.md`](CHANGELOG.md) or release notes before affected features are distributed.

## Open Privacy Questions

- What privacy disclosures will be required by future app stores and distribution channels?
- Will optional crash reporting or update checking ever be proposed, and what consent model would it require?
- Which local metadata should receive additional encryption or retention controls before public 1.0?

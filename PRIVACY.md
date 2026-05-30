# ShellKrypt: Privacy Notice

Draft status: pre-release project privacy notice.

## 1. Summary

ShellKrypt is designed as a local-only vault. By default, ShellKrypt does not create a cloud account, sync user vault data to a ShellKrypt server, collect telemetry, or send crash reports to the project owner.

The user's vault data stays in local files selected or created by the user.

## 2. Data ShellKrypt Stores Locally

ShellKrypt may store these local files:

- `.skvault` vault databases containing encrypted item payloads and encrypted vault-scoped activity logs
- `.skbx` encrypted backup packages created by the user
- plaintext JSON exports or activity report exports created by the user
- app settings such as theme, auto-lock, clipboard timeout, and first-use acknowledgement state
- vault launcher metadata such as vault display names and local vault paths
- audit dismissal state

App metadata is stored under the user's local app-data directory by default. The `SHELLKRYPT_APPROOT` environment variable can override that location for development and tests.

## 3. Data ShellKrypt Does Not Collect By Default

By default, ShellKrypt does not collect or transmit:

- master passwords or backup passphrases
- item passwords, API keys, tokens, OTP seeds, CVCs, or note contents
- vault files, backups, plaintext exports, or activity reports
- usage analytics or telemetry
- crash reports
- account identifiers from a ShellKrypt cloud service

ShellKrypt has no backend service today.

## 4. Operating System And User-Controlled Sharing

Some actions intentionally interact with the operating system:

- Copying a secret places it in the OS clipboard.
- File pickers and save dialogs expose selected file paths to the app.
- Exports and backups are written to user-selected locations.
- Users may manually share, sync, upload, or back up files outside ShellKrypt.

ShellKrypt cannot control what other apps, cloud-sync clients, backup tools, clipboard managers, malware, or device administrators do outside the app.

## 5. Mobile And Future Services

Mobile app heads are in progress. If future builds add platform store distribution, crash reporting, telemetry, cloud sync, account login, purchase validation, or support portals, this notice must be updated before those features are released.

No such ShellKrypt-hosted collection is part of the current local-only design.

## 6. Deleting Data

Users control local data deletion:

- Delete a vault through the app only when intentional and after confirming the selected `.skvault`.
- Delete backups, plaintext exports, and activity report exports from their saved locations.
- Delete app metadata from the local app-data directory if launcher/settings history should be removed.

Deleting ShellKrypt does not automatically delete every vault, backup, or export the user created elsewhere.

## 7. Security Notes

Security details are documented in `SECURITY.md` and `DISCLAIMER.md`. Important limits:

- there is no password recovery
- plaintext exports are decrypted reports
- clipboard clearing is best-effort
- secrets can exist in memory while a vault is unlocked
- ShellKrypt is not externally audited yet

## 8. Changes

This notice may change before public release. Material privacy changes should be reflected in `CHANGELOG.md` or release notes.

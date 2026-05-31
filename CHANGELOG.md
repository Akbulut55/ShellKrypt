# Changelog

Project-level changes for `ShellKrypt`.

Format follows Keep a Changelog. New changes accumulate under `Unreleased`. Before preparing a release, move relevant `Unreleased` entries into a versioned release section.

## [Unreleased]

## [ShellKrypt 0.9.9] - 2026-05-31

### Changed

- Split All Items viewmodel row, list-state, filter, and navigation behavior into focused files without changing All Items behavior.

## [ShellKrypt 0.9.8] - 2026-05-31

### Changed

- Split Web Logins viewmodel list, editor, secret, remediation, and delete behavior into focused partial files without changing Web Logins behavior.

## [ShellKrypt 0.9.7] - 2026-05-31

### Changed

- Split Markdown Notes viewmodel list, editor, autosave, preview, and delete behavior into focused partial files without changing Markdown Notes behavior.

## [ShellKrypt 0.9.6] - 2026-05-31

### Changed

- Split Credit Cards viewmodel list, editor, secret, and delete behavior into focused partial files without changing Credit Cards behavior.

## [ShellKrypt 0.9.5] - 2026-05-31

### Changed

- Split API Keys viewmodel list, editor, field, clipboard, and delete behavior into focused partial files without changing API Keys behavior.

## [ShellKrypt 0.9.4] - 2026-05-31

### Changed

- Split Authenticator viewmodel account, option, editor, QR import, delete, list-state, and code-refresh logic into focused partial files without changing Authenticator behavior.

## [ShellKrypt 0.9.3] - 2026-05-31

### Changed

- Split Welcome viewmodel launcher, vault registry, delete, security acknowledgement, and list-state logic into focused partial files without changing launcher behavior.

## [ShellKrypt 0.9.2] - 2026-05-31

### Changed

- Split Settings viewmodel transfer, master-password, option, and settings-state logic into focused partial files without changing Settings behavior.

## [ShellKrypt 0.9.1] - 2026-05-31

### Changed

- Refactored desktop navigation/list helpers and split repeated row viewmodel types out of large desktop viewmodels without changing UI behavior.

## [ShellKrypt 0.9.0] - 2026-05-31

### Added

- Added an extensible theme registry with Dark, Light, Crimson, Ocean, and Forest palettes.
- Added Settings transfer workflow tests for encrypted backup export/restore, plaintext JSON export confirmation, and CSV import.

### Changed

- Reworked desktop theme selection to use persisted theme ids and dynamically list registered themes in Settings.

## [ShellKrypt 0.8.1] - 2026-05-31

### Changed

- Filled the root README, security policy, agent instructions, and handbook documents with ShellKrypt-specific product, architecture, security, database, development, operations, decision, roadmap, and release guidance.
- Prepared GPL-3.0-or-later source licensing, public-facing notices/disclaimers, and pre-release security reporting guidance.
- Added first-use security acknowledgement to the desktop launcher before creating, importing, or opening vaults.
- Added versioning to the security acknowledgement so material terms, privacy, disclaimer, or security text changes can require re-acceptance.

### Added

- Added `LICENSE` with the full GPL v3 license text.
- Added `NOTICE.md` for official-build, modified-build, and branding expectations.
- Added `DISCLAIMER.md` covering no warranty, no password recovery, plaintext exports, clipboard limits, audit status, and regulated-data limits.
- Added `TERMS.md` and `PRIVACY.md` with draft pre-release usage and local-only privacy notices.

## [ShellKrypt 0.8.0] - 2026-05-30

### Added

- Added `ShellKrypt.Application` for shared settings, vault registry, activity log, audit dismissal, item summary, search, filter, and pagination logic.
- Added `ShellKrypt.UI.Shared` for reusable theme resources, converters, and shared controls.
- Added shared mobile shell foundation plus Android and iOS app heads.
- Added API Keys workspace with flexible dynamic fields.
- Added Authenticator workspace with TOTP/HOTP support, QR screenshot import, pasted image import, advanced options, details, edit, and delete flows.
- Added Markdown Notes workspace with source/preview switching, starred notes, create/edit/delete, and autosave after typing stops.
- Added vault-scoped encrypted activity logs with filtering, pagination, details, clearing, and plaintext report export.

### Changed

- Standardized desktop theme resources, table styling, pagination, filters, and item modal structure.
- Reworked Web Logins, Credit Cards, and API Keys modals to use shared `ModalShell`.
- Simplified the root solution layout so `ShellKrypt.slnx` is the canonical solution and mobile heads build directly by project file.
- Refactored shared app services out of Desktop into Application and Infrastructure boundaries.
- Updated README and docs to describe the pre-1.0 local-only product model.

### Fixed

- Improved small-screen desktop sidebar behavior and modal sizing.
- Fixed API key modal field overflow, note overflow, scroll boundaries, and ComboBox wheel bubbling inside field rows.
- Improved dropdown placement and scroll behavior in Settings.
- Refined table empty states, pagination, and row/footer consistency.

### Removed

- Removed the duplicate `ShellKrypt.MobileApps.slnx` root solution.
- Removed legacy/global activity-log fallback behavior from active read/write paths.
- Removed duplicated hand-rolled item modal shells from Web Logins, Credit Cards, and API Keys.

### Security

- Hardened AES-GCM encrypted blob handling with versioned envelopes and associated data where practical.
- Hardened vault path guards, deletion safety, import/export validation, and active-vault overwrite checks.
- Added stronger plaintext export confirmation and clearer decrypted-export warnings.
- Added clipboard copy disable setting and minimum clipboard timeout validation.
- Ensured activity logs are vault-scoped and encrypted, with raw secrets excluded from activity details.

## [ShellKrypt 0.1.0-alpha] - Earlier

### Added

- Added initial .NET/Avalonia desktop solution.
- Added local `.skvault` SQLite vault creation and unlock.
- Added Argon2id master-password derivation and AES-GCM encrypted item payload storage.
- Added early Web Logins, Credit Cards, Generator, Security Audit, Settings, and vault launcher workflows.

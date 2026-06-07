# Changelog

Project-level changes for `ShellKrypt`.

Format follows Keep a Changelog. New changes accumulate under `Unreleased`. Before preparing a release, move relevant `Unreleased` entries into a versioned release section.

## [Unreleased]

## [ShellKrypt 0.10.18] - 2026-06-07

### Changed

- Split Authenticator editor commands, save flow, form population, and option normalization into focused partial files without changing authenticator behavior.

## [ShellKrypt 0.10.17] - 2026-06-07

### Changed

- Split Markdown Notes display properties and property-change hooks into focused partial files without changing note behavior.

## [ShellKrypt 0.10.16] - 2026-06-07

### Changed

- Split SQLite vault transfer package validation, file, label, and KDF helpers into focused partial files without changing backup behavior.

## [ShellKrypt 0.10.15] - 2026-06-07

### Changed

- Split Welcome vault import, duplicate, remove-from-list, metadata, and default-vault commands into focused partial files without changing launcher behavior.

## [ShellKrypt 0.10.14] - 2026-06-07

### Changed

- Split Settings transfer browse, export, encrypted import, and CSV import commands into focused partial files without changing transfer behavior.

## [ShellKrypt 0.10.13] - 2026-06-07

### Changed

- Split SQLite vault transfer transaction item, label, and connection helpers into focused partial files without changing import behavior.

## [ShellKrypt 0.10.12] - 2026-06-07

### Changed

- Split credit card editor form-state and entry-mapping helpers into focused files without changing card modal behavior.

## [ShellKrypt 0.10.11] - 2026-06-07

### Changed

- Split credit card row display, notification, and formatting helpers into focused partial files without changing card table behavior.

## [ShellKrypt 0.10.10] - 2026-06-07

### Changed

- Split shared markdown block models, block parsing helpers, and inline stripping helpers into focused files without changing markdown preview behavior.

## [ShellKrypt 0.10.9] - 2026-06-07

### Changed

- Split authenticator service list, mutation, code generation, payload, and normalization behavior into focused partial files without changing authenticator behavior.

## [ShellKrypt 0.10.8] - 2026-06-07

### Changed

- Split CSV import parsing, candidate construction, type inference, record tokenization, and formatting helpers into focused files without changing import behavior.

## [ShellKrypt 0.10.7] - 2026-06-07

### Changed

- Split SQLite item repository list, label, item CRUD, and connection behavior into focused partial files without changing item persistence.

## [ShellKrypt 0.10.6] - 2026-06-07

### Fixed

- Fixed test isolation for Security Audit fingerprints and app-root metadata stores so unrelated hash text or parallel app-data overrides cannot cause random failures.

## [ShellKrypt 0.10.5] - 2026-06-06

### Changed

- Split shared vault item summary projection, counts, query, pagination, and formatting logic into focused partial files without changing list behavior.

## [ShellKrypt 0.10.4] - 2026-06-06

### Changed

- Split SQLite vault service create, unlock, password-change, schema, connection, metadata, and KDF behavior into focused partial files without changing vault behavior.

## [ShellKrypt 0.10.3] - 2026-06-06

### Changed

- Split Settings viewmodel picker, security setting, vault display, transfer state, danger-zone, and navigation behavior into focused partial files without changing Settings behavior.

## [ShellKrypt 0.10.2] - 2026-06-06

### Changed

- Split HealthAuditService web login, card, API key, settings, and helper logic into focused partial files without changing audit behavior.

## [ShellKrypt 0.10.1] - 2026-06-06

### Changed

- Split MainWindow viewmodel session, settings, clipboard, dialog, and activity behavior into focused partial files without changing desktop shell behavior.

## [ShellKrypt 0.10.0] - 2026-06-06

### Added

- Expanded Security Audit with structured local findings for web logins, credit cards, API keys, and session settings.

### Changed

- Refactored Security Audit viewmodel logic into focused scan, filter, remediation, dismissal, score, and row files.
- Reworked Security Audit UI to remove unfinished lockdown/breach controls and use real filters/actions.

## [ShellKrypt 0.9.13] - 2026-06-04

### Changed

- Updated README status wording for public source visibility and separate official build distribution.

## [ShellKrypt 0.9.12] - 2026-06-04

### Changed

- Cleaned up public-facing terms and privacy wording by removing draft-status language.

## [ShellKrypt 0.9.11] - 2026-06-01

### Fixed

- Fixed Settings theme picker option commands so registered themes can be selected from the popup.

## [ShellKrypt 0.9.10] - 2026-06-01

### Changed

- Split Activity Logs viewmodel row, list-state, filter, metadata, export, and clear-flow behavior into focused files without changing Activity Logs behavior.

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
- Added `TERMS.md` and `PRIVACY.md` with usage and local-only privacy notices.

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

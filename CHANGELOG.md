# Changelog

Project-level changes for `ShellKrypt`.

Format follows Keep a Changelog. New changes accumulate under `Unreleased`. Before preparing a release, move relevant `Unreleased` entries into a versioned release section.

## [Unreleased]

## [ShellKrypt 0.17.5] - 2026-06-28

### Added

- Added Project Secrets as an encrypted `.env` workspace with project environments, variables, API Key links, `.env` import/export/template flows, environment comparison, filesystem scanning, All Items integration, backup counts, and Security Audit findings.

### Changed

- Redesigned the Project Secrets desktop workspace around a project-header selector, read-only/edit modes, environment tabs, import/export actions, compare, scanner, and settings views.
- Changed Project Secrets variables to use inline row editing, masked value fields with eye-icon reveal controls, explicit API Key references, and drag/drop variable ordering.
- Narrowed API Keys back to a simple standalone API key/token workspace while keeping Project Secrets responsible for project-level environment variables.
- Marked Quick Fill as unfinished/experimental in public and handbook planning docs.

### Fixed

- Fixed Project Secrets empty-project display so a new vault starts with a visible New Project draft instead of a blank project title.
- Fixed Project Secrets variable rows so referenced API Keys are selected through a picker and stored as references without copying the original value.

## [ShellKrypt 0.17.4] - 2026-06-26

### Added

- Added linked credit-card fields to Quick Fill entries without duplicating card values into Quick Fill storage.

### Changed

- Replaced the Quick Fill sequence row editor with readable sequence chips and a smaller guided Add Step builder.
- Added key-combination capture for Quick Fill key steps in the Add Step builder.

### Fixed

- Kept sensitive linked field values out of Quick Fill sequence chip labels.

## [ShellKrypt 0.17.3] - 2026-06-25

### Added

- Added Quick Fill macro-style key sequence steps with modifier keys, repeats, and expanded navigation/function/letter/digit key choices.
- Added Linux X11 Quick Fill global-hotkey, target-capture, and auto-type service paths, plus XDG Desktop Portal global-shortcut registration for opening the popup on Wayland sessions.

### Changed

- Updated Quick Fill popup and manager rows to show target applications and expose row-level enable and delete actions.
- Moved the Quick Fill popup app/window scope control into the target-application header.
- Made native Wayland Quick Fill behavior explicit: portal shortcuts can open the popup, while target capture and Auto-Type remain unavailable in restricted Wayland sessions.
- Added bundled Inter and JetBrains Mono font resources, switched UI typography to the bundled Inter font, and moved code/secret displays from Consolas to JetBrains Mono.
- Removed obsolete PNG icon assets that were replaced by shared vector icon resources.
- Reworked the Quick Fill manager tab into a dashboard-style list/detail layout with summary cards, entry search/filter controls, and compact field-source tabs.
- Redesigned Quick Fill entries around explicit fill-sequence steps and a shared editor used by both the manager page and popup add/edit flow.
- Reworked vault deletion from Settings to use the shared dimmed in-page modal shell for the warning and master-password confirmation steps.
- Replaced Authenticator secret reveal text buttons with eye icons and tightened secret-key/algorithm detail text rendering.
- Changed Activity Logs from paged navigation to an internally scrollable event table.
- Changed Web Logins, Credit Cards, API Keys, and All Items from paged table navigation to internally scrollable tables.

### Fixed

- Fixed stale Quick Fill editor bindings for category, manual field creation, literal-text sequence steps, and sequence step numbering.
- Fixed the Quick Fill popup scope button spacing so it no longer sits tight against the target header edge.

## [ShellKrypt 0.17.2] - 2026-06-18

### Changed

- Updated the Quick Fill sidebar icon to the new bolt artwork, regenerated the Windows app icon as a larger white transparent glyph, and replaced visible `SK` logo badges with the main ShellKrypt icon.
- Centered the default-vault badge and made it use the active theme accent while deepening the Crimson palette from pink to calmer blood-red tones.
- Reworked the Import Vault dialog titlebar and footer so it uses ShellKrypt chrome, centered button text, and no extra footer strip.
- Moved the sidebar navigation scrollbar to the sidebar edge while keeping nav item spacing intact.

## [ShellKrypt 0.17.1] - 2026-06-17

### Fixed

- Embedded the ShellKrypt icon into the Windows desktop executable so Task Manager and process grouping use the app icon.
- Fixed Backup Center and Quick Fill desktop layouts so their content fits without horizontal page overflow at normal window sizes.

### Changed

- Reworked Backup Center into a combined backup-health dashboard with direct backup, verification, and automatic-backup actions plus a single encrypted-backup workflow panel.
- Removed the standalone Emergency Kit page and the recovery-sheet/passphrase-reminder UI from Backup Center.

## [ShellKrypt 0.17.0] - 2026-06-17

### Added

- Added desktop Quick Fill entries with encrypted vault-scoped app login/info storage, Windows hotkey popup, target matching, in-popup entry creation, selected linked fields, and user-driven fill support.
- Added a Quick Fill popup scope toggle to show either current-window matches or all entries for the captured app.
- Added optional close-to-system-tray desktop behavior with tray menu controls for opening, locking, and fully exiting ShellKrypt.

### Changed

- Renamed the desktop executable identity to `ShellKrypt` so Windows process/task labels no longer show `ShellKrypt.Desktop`.

## [ShellKrypt 0.16.1] - 2026-06-13

### Changed

- Updated public root documentation to reflect the current Backup Center, Emergency Kit, automatic-backup, localization, sidebar, and distribution wording.

## [ShellKrypt 0.16.0] - 2026-06-13

### Changed

- Grouped desktop sidebar navigation by product area and moved Settings into the active-vault sidebar footer controls.
- Reordered Settings into a single-column General, Vault Management, Security flow and moved vault destruction into the Security section.
- Made desktop table and utility screens more compact at narrower window widths by reducing page margins, card padding, table column gaps, layout minimum widths, and wide Security Audit rows.
- Added shared desktop Material icon resources and replaced sidebar initials with themed vector icons.
- Renamed the Generator navigation entry to Password Generator.

### Fixed

- Fixed the opening screen available-vaults statistic label so it no longer shows the count format placeholder.
- Fixed collapsed sidebar icon sizing and alignment so navigation, lock, and settings controls share a consistent footprint.

## [ShellKrypt 0.15.0] - 2026-06-12

### Added

- Added a desktop Emergency Kit section with recovery readiness, safe printable checklist export, and local-only recovery acknowledgements.
- Added in-app automatic encrypted backups with session-only backup passphrase handling, backup verification, retention cleanup, and Backup Center controls.

## [ShellKrypt 0.14.0] - 2026-06-11

### Added

- Added a dedicated desktop Backup Center for encrypted backups, backup verification, encrypted restore, plaintext JSON export, CSV import, and local backup history.

### Changed

- Moved backup/import/export workflows out of Settings so Settings focuses on vault security and desktop behavior.

## [ShellKrypt 0.13.0] - 2026-06-11

### Changed

- Hardened vault protection with v2-only encrypted blob handling, expanded AES-GCM associated data binding, stricter backup/activity metadata authentication, safer vault deletion guards, and broader secret-leakage tests.

## [ShellKrypt 0.12.0] - 2026-06-11

### Added

- Expanded English/Turkish runtime localization across desktop screens, dialogs, filters, modal text, empty states, and status messages.

### Changed

- Cleaned public README documentation to avoid linking to private internal handbook files.

### Fixed

- Fixed missing localization coverage for dynamic viewmodel keys and format-string status messages.

### Removed

- Removed root-level mobile documentation files from the public docs set.

## [ShellKrypt 0.11.1] - 2026-06-11

### Changed

- Moved public mobile documentation to root-level files and made the internal handbook private/untracked.

## [ShellKrypt 0.11.0] - 2026-06-10

### Added

- Added English/Turkish runtime localization foundation with persisted language selection and localized Settings screen text.

## [ShellKrypt 0.10.34] - 2026-06-07

### Changed

- Split vault transfer label schema, read, and upsert helpers into focused partial files without changing import behavior.

## [ShellKrypt 0.10.33] - 2026-06-07

### Changed

- Split web-login security audit projection and password-finding logic into focused partial files without changing audit results.

## [ShellKrypt 0.10.32] - 2026-06-07

### Changed

- Split SQLite activity log store load, append, and clear operations into focused partial files without changing encrypted log behavior.

## [ShellKrypt 0.10.31] - 2026-06-07

### Changed

- Split markdown note service listing, mutations, payload crypto, and mapping helpers into focused partial files without changing note behavior.

## [ShellKrypt 0.10.30] - 2026-06-07

### Changed

- Split crypto tools password, hashing, and Base64 helpers into focused partial files without changing tool behavior.

## [ShellKrypt 0.10.29] - 2026-06-07

### Changed

- Split web login service listing, mutations, payload crypto, and mapping helpers into focused partial files without changing login behavior.

## [ShellKrypt 0.10.28] - 2026-06-07

### Changed

- Split credit card service listing, mutations, payload crypto, and mapping helpers into focused partial files without changing card behavior.

## [ShellKrypt 0.10.27] - 2026-06-07

### Changed

- Split API key service listing, mutations, payload crypto, and mapping helpers into focused partial files without changing API key behavior.

## [ShellKrypt 0.10.26] - 2026-06-07

### Changed

- Split SQLite vault transfer export, import, and CSV workflows into focused partial files without changing transfer behavior.

## [ShellKrypt 0.10.25] - 2026-06-07

### Changed

- Split vault registry queries, mutations, and normalization helpers into focused partial files without changing registry behavior.

## [ShellKrypt 0.10.24] - 2026-06-07

### Changed

- Split shared item summary projection builders by item type without changing summary text, filtering, masking, or pagination behavior.

## [ShellKrypt 0.10.23] - 2026-06-07

### Changed

- Split SQLite item label operations, schema migration, stored-row loading, and lookup-key formatting into focused partial files without changing label behavior.

## [ShellKrypt 0.10.22] - 2026-06-07

### Changed

- Split SQLite activity log persistence, connection, and encrypted payload helpers into focused partial files without changing vault-scoped log behavior.

## [ShellKrypt 0.10.21] - 2026-06-07

### Changed

- Split Markdown Notes editor actions, save flow, and editor notifications into focused partial files without changing note behavior.

## [ShellKrypt 0.10.20] - 2026-06-07

### Changed

- Split shell viewmodel display text, navigation routing, and sidebar behavior into focused partial files without changing desktop navigation.

## [ShellKrypt 0.10.19] - 2026-06-07

### Changed

- Split All Items viewmodel state setters and display properties into focused partial files without changing dashboard behavior.

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

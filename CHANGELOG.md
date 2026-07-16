# ShellKrypt: Changelog

Public project-level change history for ShellKrypt.

Changes are grouped by release using Added, Changed, Fixed, Removed, and Security sections where they apply.

## Versioning Policy

Version format:

```text
MAJOR.MINOR.PATCH
```

- PATCH: fixes, polish, small content or behavior changes, and small internal cleanup.
- MINOR: new features, new sections, meaningful workflow changes, and notable improvements.
- MAJOR: stable release milestones, breaking changes, major redesigns, incompatible data or API changes, or a new project generation.
- Before `1.0.0`, minor versions can represent larger project iterations while ShellKrypt is still stabilizing.
- When `Unreleased` changes move into a new versioned section, update the
  corresponding application version metadata and current-version documentation
  in the same change.

## [Unreleased]

### Changed

- Added an explicit Desktop composition root and focused factories for locked
  surfaces, unlocked workspaces, and the Quick Fill popup.
- Extracted vault session, settings, dialogs, secure clipboard, activity,
  automatic backup, Quick Fill, and navigation lifecycle responsibilities from
  the main window viewmodel into focused Desktop runtime services.
- Decoupled Desktop feature viewmodels from the main window and reorganized the
  Desktop project around Shell, Bootstrap, and colocated feature workspaces.
- Added architecture checks that enforce composition ownership and prevent the
  retired lifecycle-based Desktop namespaces from returning.

## [ShellKrypt 0.24.0] - 2026-07-15

### Changed

- Split Backup Center into focused encrypted-backup, plaintext-export, CSV-import,
  automatic-backup, health, and history capabilities while preserving the
  existing single-page workflows and backup formats.
- Moved Backup Center Desktop composition into a colocated feature with focused
  workflow viewmodels and internal section controls.
- Extracted automatic-backup file naming, discovery, verification, and retention
  from Desktop lifecycle coordination into focused backup capabilities.

### Security

- Separated encrypted-package and vault-snapshot format version validation and
  rejected dangling or duplicate item-label relationships before restore mutates
  the target vault.

## [ShellKrypt 0.23.0] - 2026-07-15

### Changed

- Reworked Project Secrets around the explicit Project, Environment, Profile,
  and Variable hierarchy with nested encrypted payloads and profile-scoped scan
  results.
- Split Project Secrets contracts, application logic, persistence, and Desktop
  workspace responsibilities into focused feature components.
- Rebuilt the Project Secrets workspace with profile-scoped Variables,
  Import / Export, Compare, Scanner, and Settings views plus focused environment,
  profile, and variable editor dialogs.
- Renamed linked API Key behavior to explicit reference and import-copy flows,
  with reference values resolved only while the vault is unlocked.

### Fixed

- Fixed Project Secrets modal overlays so environment and variable dialogs cover
  the complete workspace instead of rendering beneath tab content.
- Refreshed referenced API Key values when Project Secrets opens and immediately
  after API Key changes, while keeping imported API Key copies independent.
- Removed the unused manual value input from the API Key import-copy flow and
  hardened Project Secrets payload handling against missing collection values.

### Removed

- Removed the flattened Project Secrets environment/profile compatibility model
  and redundant top-level API Key link collection.

## [ShellKrypt 0.22.0] - 2026-07-14

### Changed

- Split Authenticator persistence, OTP generation, `otpauth` URI parsing, and
  QR decoding into focused reusable capabilities across Core, Application, and
  Infrastructure.
- Reorganized the Desktop Authenticator as a colocated feature with a dedicated
  editor viewmodel and navigation-scoped code refresh lifecycle.
- Updated Quick Fill to resolve Authenticator entries and one-time codes through
  the focused Authenticator contracts.

### Removed

- Removed the duplicate Authenticator details dialog in favor of the live
  workspace details pane.

## [ShellKrypt 0.21.0] - 2026-07-14

### Changed

- Updated Avalonia across Desktop, shared UI, and mobile projects from 12.0
  to 12.1, including the Linux DBus dependency required by Avalonia 12.1.
- Rebuilt the Desktop design system around Dark and Light semantic palettes,
  modular shared style dictionaries, standardized compact controls, and
  enforceable contrast and XAML styling rules.
- Migrated ordinary Desktop inputs, search fields, read-only secret surfaces,
  and button aliases to shared `sk-*` component roles.
- Rebuilt the Credit Card details preview with a scalable theme-backed
  gradient and depth elements instead of a bitmap asset.
- Unified Dark and Light primary actions on a shared cyan accent while
  retaining a darker accessible accent text role for Light surfaces.

### Fixed

- Separated Credit Card number and CVC reveal controls and replaced
  font-dependent CVC asterisks with consistent masking bullets.

### Removed

- Removed the Crimson, Ocean, and Forest themes and the obsolete legacy theme
  mode compatibility field.

## [ShellKrypt 0.20.0] - 2026-07-12

### Changed

- Reorganized Web Logins, Credit Cards, and API Keys into colocated Desktop
  feature folders with shared workspace headers, toolbars, responsive card
  grids, editor state, and modal footer controls.
- Replaced the three item tables and large metric panels with responsive cards,
  compact findings summaries, and consistent add, details, edit, cancel, save,
  and delete-confirmation flows.
- Updated Web Login password generation to use the reusable Crypto Tools
  password generator instead of a feature-local implementation.
- Standardized Web Login, Credit Card, and API Key detail dialogs around a
  responsive identity-and-details layout with masked secret rows, feature
  icons, local-encryption status, and consistent Delete, Close, and Edit
  actions.
- Refined the shared modal and Dark theme presentation with neutral charcoal
  surfaces, cyan interaction accents, clearer elevation hierarchy, and
  semantic read-only secret and credit-card preview surfaces.

## [ShellKrypt 0.19.1] - 2026-07-11

### Added

- Added structured private maintainer documentation for project direction,
  engineering practices, architecture, storage and file formats, cryptography,
  threat modeling, data handling, the in-app Security Audit, and temporary
  technical findings.

### Changed

- Replaced overlapping legacy planning notes with subject-owned documentation
  rules, explicit authority boundaries, and focused cross-references.
- Reworked the public README, security policy, privacy notice, legal notices,
  disclaimer, and terms so public documents no longer depend on private
  maintainer material.
- Updated repository ignore rules for the private documentation tree, encrypted
  backup packages, local environment files, and the new Desktop source path.

## [ShellKrypt 0.19.0] - 2026-07-10

### Changed

- Reorganized production projects under `src/`, tests under `tests/`, and added a `docs/` entry point while keeping the root solution canonical.

## [ShellKrypt 0.18.0] - 2026-07-10

### Changed

- Split Crypto Tools into reusable password generation, password strength, hashing, and Base64 capabilities while keeping them together in one desktop workspace.
- Renamed the Password Generator workspace, navigation identifiers, localization keys, and activity category to Crypto Tools.
- Simplified password strength ratings to None, Weak, Fair, and Strong.

## [ShellKrypt 0.17.10] - 2026-07-03

### Added

- Added configurable Markdown Notes autosave timing in Settings.
- Added API Key user metadata and expanded sorting options for Web Logins, Credit Cards, and API Keys.
- Added shared desktop ComboBox and rich picker style variants for settings selectors, filter dropdowns, modal fields, compact selectors, and searchable item pickers.

### Changed

- Standardized Settings dropdowns on native ComboBox controls instead of one-off button popup selectors.
- Updated Web Logins, Credit Cards, API Keys, Quick Fill, modal forms, and Project Secrets selectors to use shared picker and ComboBox style classes.
- Refined table, filter, search, and action layouts across item workspaces, All Items, Activity Logs, Security Audit, Authenticator, and Password Generator.
- Updated Markdown Notes picker and Project Secrets picker popups to share picker styling while keeping their searchable card picker behavior.
- Updated Password Generator strength, slider, toggle, and selection colors to follow theme resources.

### Fixed

- Fixed Settings selector spacing and Security Profile dropdown styling so it matches other Settings selectors.
- Fixed blank-view regressions from overly broad ComboBox styling by limiting global selector variants to safe property overrides.
- Fixed Activity Logs layout so the table and side panels move together toward the bottom edge.
- Removed vault path display from Activity Log event metadata.
- Removed bottom-edge coloring from Security Audit summary cards.

## [ShellKrypt 0.17.9] - 2026-07-01

### Changed

- Refined Markdown Notes picker rows to show only note names with icon-only delete actions.
- Changed Markdown Notes so new drafts open in editor mode, while saved and selected notes open in preview mode.
- Thinned Markdown Notes editor and preview pane framing for a cleaner writing surface.

### Fixed

- Removed the default thick flyout chrome from Markdown Notes and Project Secrets picker popups.
- Fixed the Markdown Notes editor/preview mode tooltip to describe the preview toggle instead of the editor.

## [ShellKrypt 0.17.8] - 2026-06-30

### Added

- Added a user metadata field to standalone API Key entries and summaries.

### Changed

- Rebuilt Markdown Notes into a fixed-header markdown workspace with a note picker, split/editor/preview modes, dirty-state cancel/save actions, and a project-style note selection popup.
- Updated Markdown Notes mode controls to use separate split and editor/preview icon buttons with the new markdown mode icons.
- Updated Web Logins, Credit Cards, API Keys, and All Items table layouts so their tables stretch cleanly toward the bottom edge.
- Replaced text copy actions with shared copy icons across Web Logins, Credit Cards, and API Keys.
- Added short workspace descriptions to Web Logins, Credit Cards, and API Keys for consistency with Authenticator.

### Fixed

- Fixed Markdown Notes picker placement, note-row selection, and duplicate editor title input layout.

## [ShellKrypt 0.17.7] - 2026-06-30

### Added

- Added reusable desktop `sk-*` button classes, themed dropdown glyphs, themed toggle icons, and theme-aware text selection brushes.
- Added favorite vault support on the welcome screen so multiple favorite vaults can be promoted ahead of the rest of the vault list.

### Changed

- Standardized desktop action, icon, table, picker, chip, modal, and dialog buttons around shared global styling instead of per-view button variants.
- Reworked Project Secrets environment/profile management around centered popups, user-defined profile names, selected environment/profile indicators, and icon-only row actions.
- Updated Settings controls to use shared section layout, compact dropdowns, and real icons instead of placeholder badges.
- Updated Password Generator controls to use themed slider, toggle, copy, regenerate, strength, and selection colors.
- Improved Authenticator layout responsiveness, secret reveal icon placement, and add-code modal sizing.

### Fixed

- Fixed Project Secrets profile selection so selecting an already active profile keeps its variables visible instead of clearing the list.
- Fixed extra hover borders on chip buttons and inconsistent vertical alignment in filters, search bars, activity filters, and row actions.
- Fixed several icon-button alignment issues across Password Generator, Authenticator, item tables, dialogs, and shared modal controls.

## [ShellKrypt 0.17.6] - 2026-06-29

### Changed

- Changed Project Secrets environment creation to use a centered modal flow with user-defined profile names instead of fixed profile presets.
- Changed Project Secrets environment management to use a single centered Environments popup with per-environment detail controls for profiles and deletion.
- Normalized the encrypted Project Secrets payload into separate environment, profile, and variable sections while keeping storage in the existing encrypted item payload model.

### Fixed

- Fixed Project Secrets import destination controls so `.env` imports show the selected environment and profile target.
- Fixed the Project Secrets project root label punctuation and tightened the environment/profile selector layout.

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

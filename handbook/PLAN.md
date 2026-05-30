# ShellKrypt: Engineering Plan

This plan turns the product direction in `handbook/IDEA.md` into implementable engineering slices. Product language belongs in `handbook/IDEA.md`; execution detail, system boundaries, and verification belong here.

Main implementation assumption:

> Optimize first for a reliable Windows desktop local vault, while keeping shared application and mobile foundations clean enough for Android/iOS expansion.

## 1. Engineering Scope

The current implementation should deliver:

- A working local encrypted desktop vault.
- Consistent item management for web logins, credit cards, API keys, authenticators, and markdown notes.
- Secure unlock, backup, restore, plaintext export warning, clipboard controls, and vault-scoped activity logs.
- A shared application layer and mobile shell foundation for future mobile app work.

This plan does not include:

- Cloud sync, account system, team sharing, or remote password recovery.
- Public release operations such as code signing, installers, store submission, terms/privacy/disclaimer docs, and paid support.
- A completed mobile product with all desktop workflows.

## 2. Fixed Implementation Decisions

- First surface: Windows desktop.
- First runtime: .NET 10.
- First storage model: local SQLite `.skvault` files.
- First authentication model: local master-password unlock with Argon2id-derived key material.
- First deployment target: local desktop publish artifacts.
- First source-of-truth document: `handbook/IDEA.md`.
- Stack decisions live in: `handbook/TECH_STACK.md`.
- Security rules live in: `SECURITY.md`.

## 3. Current Repo State

Working pieces:

- Desktop create/import/open/delete/default vault flows.
- Unlock screen with password reveal and Enter-to-unlock behavior.
- All Items dashboard with summaries, search, filters, pagination, and cross-item overview.
- Web Logins, Credit Cards, API Keys, Authenticator, Markdown Notes, Generator, Security Audit, Settings, and Activity Logs screens.
- AES-GCM encrypted payloads, Argon2id unlock, encrypted backups, plaintext export warnings, path guards, import validation, and vault-scoped encrypted activity logs.
- `ShellKrypt.Application` shared services for registry/settings/activity/audit/item summary logic.
- `ShellKrypt.UI.Shared` shared theme/control primitives, including `ModalShell`.
- Shared mobile shell plus Android/iOS heads.

Known limitations:

- Mobile app is not feature-complete.
- Desktop platform validation beyond Windows is incomplete.
- External security audit has not been performed.
- Commercial release work remains: signing, installer, update channel, terms/privacy/disclaimer docs, support, export-compliance review, language localization.

## 4. Target Architecture

```text
Desktop or Mobile Shell
  -> ShellKrypt.Application shared use-cases
  -> ShellKrypt.Core domain models/contracts
  -> ShellKrypt.Infrastructure persistence/crypto/import/export
  -> Platform adapters for clipboard, files, lifecycle, QR/image import
```

Rules:

- UI code does not own domain decisions.
- Domain and application code do not import platform-specific UI.
- Infrastructure owns SQLite, crypto helpers, file-backed stores, import/export, and path guards.
- Desktop keeps window, dialog, file picker, clipboard, and platform behavior.
- Mobile uses shared application logic but replaces tables/modals with mobile pages/lists.

## 5. Repository Boundaries

| Directory | Responsibility |
|---|---|
| `ShellKrypt.Core` | Domain models, payload records, service interfaces, security settings, transfer models |
| `ShellKrypt.Application` | Shared use-cases, settings, registry, activity, audit dismissal, item summaries, filters |
| `ShellKrypt.Infrastructure` | SQLite, crypto, backup/restore/import/export, file stores, path guards |
| `ShellKrypt.UI.Shared` | Shared theme resources, controls, converters, navigation metadata |
| `ShellKrypt.Desktop` | Avalonia desktop shell, views, viewmodels, dialogs, desktop services |
| `ShellKrypt.Mobile` | Shared mobile UI and mobile viewmodels |
| `ShellKrypt.Mobile.Android` | Android app head |
| `ShellKrypt.Mobile.iOS` | iOS app head |
| `ShellKrypt.Tests` | xUnit tests |
| `handbook` | Product, planning, technical, operations, and decision documents |

## 6. First Vertical Slice

The original vertical slice is now implemented:

```text
Create vault
  -> unlock with master password
  -> add encrypted web login
  -> lock vault
  -> reopen and unlock
  -> verify item persists and decrypts locally
```

Acceptance criteria:

- Vault file is created with metadata and encrypted vault key.
- Item payload is encrypted at rest.
- Wrong master password cannot unlock the vault.
- Reopening with the correct master password restores the item.

## 7. Domain Contracts

Key contracts are C# types in `ShellKrypt.Core` and `ShellKrypt.Application`:

- `ItemType`
- item payload records for web/card/note/authenticator/API key records
- `IVaultService`
- `IVaultTransferService`
- `VaultItemSummary`
- `ItemListQuery`
- `PagedResult<T>`
- `AppSettings`
- `SessionSecuritySettings`

Domain rules:

- Item type IDs are stable because they are stored in the vault.
- Sensitive payloads are encrypted before storage.
- Application services expose shared logic without Avalonia or platform APIs.

## 8. Persistence Plan

Current vault tables:

- `vault_meta`
- `items`
- `labels`
- `item_labels`
- `activity_logs`

Current app metadata files:

- `settings.json`
- `vaults.json`
- `audit-dismissals.json`

Rules:

- Generated local database files are not committed.
- App metadata must not store item secrets.
- Changes to vault format, crypto envelope, or KDF metadata require tests and migration notes.

## 9. Adapter Plan

Platform behavior should sit behind small ports/adapters:

```csharp
public interface IClipboardPort;
public interface IAppPathProvider;
public interface IAppSettingsStore;
public interface IVaultRegistryStore;
public interface IActivityLogStore;
public interface IAuditDismissalStore;
```

Mobile-specific adapters still need real implementations for:

- clipboard and clipboard clearing limitations
- app-private storage
- file picker/share sheet
- image and QR import
- lifecycle lock behavior
- privacy screen/screenshot protection where supported
- optional biometric unlock as convenience only

## 10. Guardrails

The system must never:

- Claim password recovery exists.
- Store raw secrets in settings, registry, audit dismissal state, activity logs, source control, or committed fixtures.
- Export plaintext without explicit user confirmation and warning.
- Delete or overwrite unexpected vault paths.
- Make unsupported security claims.
- Treat clipboard clearing as a security boundary.

## 11. Phase Plan

### Phase 0 - Architecture Foundation

Status: done.

Acceptance:

- `ShellKrypt.Application` owns shared app logic.
- `ShellKrypt.UI.Shared` owns reusable visual primitives.
- Root solution layout is simplified.

### Phase 1 - Desktop Product Stabilization

Status: active.

Acceptance:

- Desktop item screens share consistent table, filter, modal, pagination, and theme behavior.
- Settings, backup/restore, plaintext export, clipboard, and activity logs behave consistently.
- Small-screen desktop layouts remain usable.

### Phase 2 - Mobile MVP

Status: planned.

Acceptance:

- Android app creates and unlocks app-private vaults.
- Mobile All Items and Web Login list/detail/add/edit flows are real.
- Mobile settings includes lock/clipboard security behavior.

### Phase 3 - Release Hardening

Status: planned.

Acceptance:

- Windows signing/installer/update path is decided.
- Security copy is limited and accurate.
- Dependency vulnerability checks and release smoke tests are documented.
- Legal text, support channel, and export-compliance review are ready.

## 12. Test Plan

Unit tests:

- settings/session normalization
- item summary search/filter/sort/pagination
- weak/reused password counts
- markdown parsing

Integration tests:

- vault create/unlock/change password
- AES-GCM tamper/wrong key/truncated blob cases
- encrypted backup export/import
- plaintext export guard behavior
- CSV import parsing and duplicate strategies
- activity log encryption and sanitization
- path/deletion safety

Manual tests:

- desktop create/open/unlock/delete vault
- all item type add/edit/delete/detail flows
- clipboard copy and copy-disabled mode
- activity report export and log clearing
- Android launch and base vault flow

## 13. Engineering Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Desktop viewmodels remain too large | Harder mobile reuse and regression risk | Continue moving shared logic into Application and focused services |
| Mobile copies desktop UI patterns | Poor small-screen usability | Use mobile lists/pages and platform adapters |
| Security changes regress compatibility | Data loss or unlock failures | Add tests before format/KDF changes |
| Release work is underestimated | Commercial launch delay | Track signing, installer, support, terms/privacy/disclaimer docs, and compliance separately |

## 14. Open Engineering Questions

- Should `SettingsViewModel`, `WelcomeViewModel`, and `AuthenticatorViewModel` be split further before mobile work resumes?
- Should desktop macOS/Linux be validated before or after mobile MVP?
- What is the release packaging strategy for Windows?
- Which localization framework should be used for non-English UI?

## 15. Verification Commands

```powershell
dotnet build .\ShellKrypt.slnx
dotnet test .\ShellKrypt.slnx
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

When checking iOS:

```powershell
dotnet build .\ShellKrypt.Mobile.iOS\ShellKrypt.Mobile.iOS.csproj -f net10.0-ios
```

The iOS command requires a supported Apple build environment and iOS workload.

## 16. Near-Term Implementation Order

1. Continue desktop UI consistency work for item screens and settings.
2. Split remaining large desktop viewmodels where shared logic can move to Application.
3. Build real mobile Web Login list/detail/add/edit.
4. Add mobile settings security pages.
5. Expand mobile item support to Notes, Cards, API Keys, Authenticator, backup/restore/export.
6. Start release hardening: signing, installer, smoke tests, terms/privacy/disclaimer docs, export-compliance review.

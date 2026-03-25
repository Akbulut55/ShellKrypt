# ShellKrypt Delivery Plan

## Goal

Deliver the remaining `v1` scope for ShellKrypt as a local-first desktop password manager with a secure vault lifecycle, feature-complete item management, useful tooling, and enough validation to keep the codebase stable while we build.

## Current Baseline

Already working:

- local vault creation
- vault unlock and manual lock flow
- encrypted SQLite persistence
- encrypted CRUD for web logins, cards, and secure notes
- shell navigation between major sections

Current gaps visible in code:

- `All Items` is still a placeholder
- `Settings` is still a placeholder
- labels / filters are not wired
- tools page is not built
- health page is not built
- auto-lock and clipboard timeout are not built
- some payload fields exist but are not exposed in UI
- there is no visible test project yet

## Delivery Principles

- Keep the app local-only.
- Keep encryption/decryption centralized.
- Decrypt only after unlock and clear sensitive session state on lock.
- Prefer shared services and reusable view models over duplicating item logic.
- Keep worker ownership disjoint to reduce merge conflicts.

## v1 Target Features

### Must have

- `All Items` page with counts, search, type filter, and navigation into the right section/item
- labels and filters
- settings page with at least auto-lock and clipboard timeout settings
- clipboard copy helpers with timed clearing
- auto-lock on idle and/or app deactivation
- tools page with password generator and hashing/base64 helpers
- health page with weak / reused / old password analysis
- fuller payload editing surface for existing item types
- test coverage for core crypto/repository/view-model flows

### Nice to have if time allows

- favorites surfaced in UI
- better empty states and error states
- more polished filtering and sort behavior

## Ownership Model

Three worker agents will build in parallel. Each worker must stay inside its owned area unless a dependency makes a tiny edit unavoidable. If a worker must touch a shared file, keep the change minimal and note it clearly.

### Worker 1: Core Security + Settings

Primary responsibility:

- vault session lifecycle and security-sensitive desktop behavior

Owned files and areas:

- `ShellKrypt.Desktop/Services/*`
- `ShellKrypt.Desktop/ViewModels/MainWindowViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/UnlockViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/CreateVaultViewModel.cs`
- new settings view / viewmodel files
- app-level wiring needed for settings, auto-lock, and clipboard timeout

Target outcomes:

- settings page replaces placeholder
- auto-lock timeout configurable
- minimize/deactivate lock behavior implemented if practical in current Avalonia shell
- clipboard copy service with timed clearing
- better clearing of sensitive session data on lock
- add or update tests in this slice if a test project exists or is created by main thread

### Worker 2: All Items + Labels + Search

Primary responsibility:

- unified item index, labels, filters, and cross-item search/navigation

Owned files and areas:

- `ShellKrypt.Core/Items/*`
- `ShellKrypt.Infrastructure/Items/*`
- new shared item-query/index models if needed
- new `All Items` view / viewmodel files
- `ShellKrypt.Desktop/ViewModels/ShellViewModel.cs` for replacing the placeholder page
- related XAML view registration for `All Items`

Target outcomes:

- `All Items` page replaces placeholder
- top summary counts for web/cards/notes
- unified search across loaded decrypted items
- type filter and label filter
- row click routes user into the proper page and selects or focuses the item if practical
- repository support for labels and item-label assignment

### Worker 3: Tools + Health + Item Form Completion

Primary responsibility:

- user-facing productivity features and completion of existing item forms

Owned files and areas:

- `ShellKrypt.Desktop/ViewModels/WebLoginsViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/CardsViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/SecureNotesViewModel.cs`
- related `Views/*.axaml` for those sections
- new tools view / viewmodel files
- new health view / viewmodel files
- `ShellKrypt.Desktop/ViewModels/ShellViewModel.cs` only for nav additions if unavoidable

Target outcomes:

- tools page added with password generator, SHA-256, SHA-512, base64 encode/decode
- health page added with reused / weak / old password checks
- web login form exposes `Url`, `Notes`, `TwoFaNote`
- card form exposes `Notes`
- note form reviewed and improved if needed for parity and usability
- tests in this slice if test scaffolding exists

## Main Thread Responsibilities

The main thread will coordinate and may touch shared integration files when needed:

- create and adjust `plan.md`
- create test project scaffolding if needed
- integrate worker results
- run builds/tests
- resolve conflicts in shared shell/navigation areas

## Suggested Build Order

1. Worker 2 builds the shared item index shape and `All Items` page scaffold.
2. Worker 1 builds settings/session services in parallel.
3. Worker 3 builds tools/health and expands item forms in parallel.
4. Main thread integrates shell navigation overlaps.
5. Main thread adds or finalizes tests and verification if workers have not already done so.

## Acceptance Criteria

The project is considered complete for this pass when:

- the app builds successfully
- `All Items`, `Settings`, `Tools`, and `Health` are no longer placeholders
- labels can be created/assigned and used for filtering
- search works across the unlocked in-memory item set
- auto-lock and clipboard timeout function in at least one clear, testable desktop scenario
- existing CRUD flows still work for web logins, cards, and notes
- fuller item payload fields are editable and persist correctly
- at least one automated test project exists with meaningful coverage of critical paths

## Working Rules For All Workers

- Read this file before coding.
- You are not alone in the codebase; other workers are making changes in parallel.
- Do not revert or overwrite another worker's edits.
- If you encounter another worker's changes in a shared file, adapt to them.
- Keep comments concise.
- Prefer adding small reusable types/services instead of large monolithic view models.
- Report changed file paths in your final response.

## Verification

Preferred commands for the main thread after integration:

```powershell
dotnet build ShellKrypt.slnx
dotnet test ShellKrypt.slnx
```

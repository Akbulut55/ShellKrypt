# ShellKrypt Core v2 Delivery Plan

## Goal

Turn ShellKrypt from a solid local-first encrypted vault into a more complete desktop password manager product by delivering the `core v2` scope only:

- multi-vault management
- import / export / backup workflows
- integrated authenticator / TOTP support
- richer item modeling and detail UX
- stronger desktop polish around startup, empty states, and favorites

Stretch v2 is intentionally out of scope for this pass.

## Current Baseline

Already working from v1:

- local vault creation
- unlock / manual lock flow
- encrypted SQLite persistence
- encrypted CRUD for web logins, cards, and secure notes
- all-items navigation
- labels / filters
- tools / health / settings
- auto-lock and clipboard timeout

Current gaps relative to v2:

- startup still assumes a single default vault
- vaults are not managed as first-class user-facing records
- no recent vault list or vault metadata UX
- no import / export / restore workflow
- no CSV import
- web items do not support integrated TOTP generation
- richer favorites / pinned workflows are incomplete
- item detail experience is still relatively lightweight
- there is still no automated test project in the solution

## Scope For This Pass

### In scope

- multiple vault support
- startup vault manager / selector
- recent vaults
- vault metadata:
  - display name
  - description
  - accent color or icon if practical
  - created date
  - optional default vault
- native encrypted ShellKrypt export
- native encrypted ShellKrypt restore / import
- plaintext JSON export with warnings
- CSV import with preview and duplicate strategy
- TOTP secret support on Web items
- current OTP code generation and countdown
- copy TOTP code action
- richer Web / Card / Note detail experience
- favorite / pinned UI surfacing if practical within existing model
- startup / empty / error state polish
- tests for import / export and TOTP logic

### Explicitly deferred

- attachments
- Bitwarden / 1Password importers
- identity item type
- passkey features
- biometrics / Windows Hello
- sync / browser / cloud / team features

## Delivery Principles

- Keep the app local-only.
- Keep encryption centralized and reusable.
- Treat vaults as first-class records, not hardcoded paths.
- Preserve the secure unlock lifecycle and clear sensitive in-memory data on lock.
- Prefer shared services and data models over duplicated feature logic.
- Keep worker ownership disjoint and use the main thread for integration.

## Ownership Model

Three worker agents will implement core v2 in parallel. Each worker owns its slice and should avoid broad edits outside it. If a shared file must be touched, keep the change minimal and clearly note it.

### Worker 1: Vault Management + Startup UX

Primary responsibility:

- make vaults user-facing, discoverable, and switchable

Owned files and areas:

- `ShellKrypt.Desktop/Services/*` related to vault registry, recent vaults, metadata, and default vault selection
- `ShellKrypt.Desktop/ViewModels/MainWindowViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/WelcomeViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/UnlockViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/CreateVaultViewModel.cs`
- new startup / vault manager viewmodels and views
- minimal app-shell wiring needed for vault switching

Target outcomes:

- startup page becomes a vault manager / selector
- user can create, open, and switch between multiple vaults
- recent vaults list exists
- vault metadata can be viewed and edited
- one vault can be marked as default
- better startup / empty / error states

### Worker 2: Import / Export / Backup + Tests

Primary responsibility:

- portable vault data flows and validation coverage

Owned files and areas:

- `ShellKrypt.Core/Vaulting/*`
- `ShellKrypt.Infrastructure/Vaulting/*`
- import / export / backup service models and implementations
- CSV import mapping logic
- any new import / export viewmodels and views
- test project creation and tests for import / export flows if needed

Target outcomes:

- encrypted ShellKrypt-native export
- encrypted ShellKrypt restore / import
- plaintext JSON export with strong warning messaging
- CSV import with preview and duplicate handling
- tests for import / export parsing and round-tripping

### Worker 3: TOTP + Richer Items + Detail UX

Primary responsibility:

- make credential items feel product-like rather than minimal

Owned files and areas:

- `ShellKrypt.Core/Items/*`
- `ShellKrypt.Infrastructure/Items/*` only where item storage shape must evolve
- `ShellKrypt.Desktop/ViewModels/WebLoginsViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/CardsViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/SecureNotesViewModel.cs`
- `ShellKrypt.Desktop/ViewModels/AllItemsViewModel.cs`
- related item views
- new TOTP helper logic and any tests in this slice if test scaffolding exists

Target outcomes:

- Web items support TOTP secret storage
- current OTP code and countdown display exist
- copy TOTP action exists
- Web / Card / Note details feel richer and more complete
- favorites / pinned state is surfaced where practical
- tests for TOTP parsing / generation

## Main Thread Responsibilities

The main thread coordinates and integrates:

- maintain `plan.md`
- resolve shared shell / navigation overlaps
- review worker outputs
- fix integration compile issues
- run `dotnet build` and `dotnet test`
- patch solution wiring for any new test projects

## Suggested Execution Order

1. Worker 1 upgrades startup into a vault manager and introduces vault metadata / recents.
2. Worker 2 builds portable data services and test scaffolding in parallel.
3. Worker 3 upgrades Web item modeling with TOTP and improves item detail UX in parallel.
4. Main thread integrates shell overlaps, fixes compile issues, and verifies build / tests.

## Acceptance Criteria

This pass is complete when:

- the app builds successfully
- startup is multi-vault aware
- recent vaults and default vault behavior work
- native encrypted export and restore work
- plaintext JSON export and CSV import work with clear warnings / preview behavior
- Web logins support TOTP secret storage and current code generation
- richer item details are visible and persist correctly
- favorites / pinned behavior is surfaced if implemented in this pass
- at least one automated test project exists
- import / export and TOTP logic have meaningful automated coverage

## Working Rules For All Workers

- Read this file before coding.
- You are not alone in the codebase; other workers are making changes in parallel.
- Do not revert or overwrite another worker's edits.
- If shared files change, adapt rather than reverting.
- Keep comments concise.
- Prefer reusable services / models over one-off feature code.
- Report changed file paths in the final response.

## Verification

Preferred commands for the main thread after integration:

```powershell
dotnet build ShellKrypt.slnx
dotnet test ShellKrypt.slnx
```

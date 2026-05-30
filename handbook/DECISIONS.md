# ShellKrypt: Decisions

This document records durable project decisions. Use it when a choice would be expensive to reverse, likely to be questioned later, or important for future contributors and coding agents to understand.

## Decision Index

| ID | Title | Status | Date |
|---|---|---|---|
| D-0001 | Local-only vault model | Accepted | 2026-05-30 |
| D-0002 | SQLite `.skvault` vault files | Accepted | 2026-05-30 |
| D-0003 | AES-GCM payload encryption and Argon2id unlock | Accepted | 2026-05-30 |
| D-0004 | No password recovery | Accepted | 2026-05-30 |
| D-0005 | Shared Application and UI.Shared layers | Accepted | 2026-05-30 |
| D-0006 | One shared mobile shell with platform heads | Accepted | 2026-05-30 |
| D-0007 | One canonical root solution | Accepted | 2026-05-30 |
| D-0008 | GPL source license with official-build monetization | Accepted | 2026-05-31 |

## Status Values

- Proposed: the decision is being considered.
- Accepted: the decision is active.
- Superseded: a later decision replaced it.
- Rejected: the option was considered and intentionally not chosen.

## D-0001 - Local-only vault model

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `handbook/IDEA.md`, `SECURITY.md`

### Context

The product is for users who want sensitive records on their own device without a ShellKrypt cloud account.

### Decision

ShellKrypt is local-only by default. There is no cloud account, sync service, server-hosted vault, or remote account recovery.

### Rationale

- Reduces trust placed in a remote service.
- Keeps the first product scope understandable.
- Aligns with no-password-recovery and user-controlled backups.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Cloud sync first | Adds account, server, breach, compliance, and recovery scope |
| Browser extension first | Requires a different security and platform model |

### Consequences

Positive:

- Strong privacy story for local users.
- Simpler infrastructure.

Negative or tradeoffs:

- Users must manage backup and device transfer.
- Multi-device sync is not automatic.

### Review Trigger

Revisit when cloud sync becomes a committed product milestone.

## D-0002 - SQLite `.skvault` vault files

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `handbook/DATABASE.md`, `handbook/TECH_STACK.md`

### Decision

Vaults are stored as local SQLite `.skvault` files.

### Rationale

- SQLite is portable and reliable for a single local vault file.
- The schema can store metadata, items, labels, and activity logs together.
- Backup/import/export can operate on a file-oriented model.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Loose encrypted JSON files | More fragile for transactional updates and indexing |
| Server database | Conflicts with local-only product model |

### Consequences

Positive:

- Simple local backup and file ownership model.
- Works with encrypted payload storage.

Negative or tradeoffs:

- Schema compatibility needs care.
- Users can move/delete files outside the app.

### Review Trigger

Revisit when cloud sync or multi-user sharing becomes a real requirement.

## D-0003 - AES-GCM payload encryption and Argon2id unlock

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `SECURITY.md`, `handbook/TECH_STACK.md`

### Decision

Sensitive payloads use AES-GCM encrypted blobs. Master passwords derive unlock material using Argon2id.

### Rationale

- AES-GCM provides authenticated encryption.
- Argon2id is appropriate for password-based key derivation.
- Central helpers reduce inconsistent crypto behavior.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Plain SQLite encryption only | Less explicit per-payload ownership and migration control |
| Custom crypto | Too risky and unnecessary |

### Consequences

Positive:

- Tampering and wrong-key cases can be tested directly.
- Payload encryption is independent of SQLite internals.

Negative or tradeoffs:

- KDF settings affect unlock performance.
- Format changes require compatibility handling.

### Review Trigger

Revisit after external security review or if standards/tooling change materially.

## D-0004 - No password recovery

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `README.md`, `SECURITY.md`

### Decision

ShellKrypt does not provide master-password recovery.

### Rationale

- Local-only encryption means the app developer should not hold recovery material.
- Any recovery mechanism would weaken the product promise or require a server/account model.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Recovery key held by developer | Violates local-only trust model |
| Security questions | Weak recovery path |
| Cloud recovery | Requires account/server model |

### Consequences

Positive:

- Clear security boundary.
- No hidden remote recovery trust.

Negative or tradeoffs:

- Forgotten master password can cause permanent data loss.
- Backup education is critical.

### Review Trigger

Revisit only if the product direction changes away from local-only storage.

## D-0005 - Shared Application and UI.Shared layers

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `handbook/PLAN.md`

### Decision

Shared non-platform logic lives in `ShellKrypt.Application`. Shared visual resources and reusable controls live in `ShellKrypt.UI.Shared`.

### Rationale

- Desktop code had begun to own reusable logic.
- Mobile needs the same settings, registry, activity, summaries, filters, and theme primitives.
- Boundaries reduce duplicate platform-specific implementations.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Keep logic in Desktop | Blocks mobile reuse and increases viewmodel size |
| Put UI controls in Application | Would pollute application logic with Avalonia UI |

### Consequences

Positive:

- Clearer dependency direction.
- Easier mobile expansion.

Negative or tradeoffs:

- Refactors require careful regression testing.

### Review Trigger

Revisit if shared layers become dumping grounds instead of focused boundaries.

## D-0006 - One shared mobile shell with platform heads

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `ShellKrypt.Mobile/README.md`, `handbook/ROADMAP.md`

### Decision

ShellKrypt uses one shared mobile UI project with Android and iOS app heads.

### Rationale

- Avoids separate Android/iOS product implementations.
- Keeps mobile UX consistent while allowing platform adapters where needed.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Separate Android and iOS shells | Duplicates product logic and UI decisions |
| Desktop UI reused directly | Tables/modals do not translate well to small screens |

### Consequences

Positive:

- Shared mobile implementation path.
- Platform differences stay behind app heads/adapters.

Negative or tradeoffs:

- Some platform-native behavior may require careful adapter design.

### Review Trigger

Revisit if platform constraints make a shared shell impractical.

## D-0007 - One canonical root solution

- Status: Accepted
- Date: 2026-05-30
- Owner: Project owner
- Related documents: `README.md`, `handbook/DEVELOPMENT.md`

### Decision

`ShellKrypt.slnx` is the only root solution. Mobile platform heads are built directly by project file.

### Rationale

- Multiple root solutions made the repo look ambiguous.
- Including iOS in the default solution would require optional iOS workload/tooling on Windows.
- Direct project builds keep mobile heads available without breaking the normal desktop/test flow.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Keep `ShellKrypt.MobileApps.slnx` | Looked like duplicate project structure |
| Put Android/iOS heads in root solution | iOS workload/tooling breaks default Windows build |

### Consequences

Positive:

- One clear canonical solution.
- Default build remains Windows-friendly.

Negative or tradeoffs:

- Mobile head builds require explicit project commands.

### Review Trigger

Revisit if .NET solution tooling supports optional platform heads without breaking default builds.

## D-0008 - GPL source license with official-build monetization

- Status: Accepted
- Date: 2026-05-31
- Owner: Project owner
- Related documents: `LICENSE`, `NOTICE.md`, `TERMS.md`, `PRIVACY.md`, `DISCLAIMER.md`, `README.md`, `SECURITY.md`

### Context

ShellKrypt is security-sensitive vault software. Users need transparency to trust the encryption and local-only behavior, but the project owner also wants a path to sell convenient official builds without committing to support-heavy subscription promises.

### Decision

Prepare ShellKrypt source for `GPL-3.0-or-later` release. Monetization should focus on official signed builds, distribution convenience, optional donations, and future support/services rather than hiding the source.

### Rationale

- Source transparency is valuable for a vault app.
- GPL keeps derivatives open when distributed.
- Official signed builds remain useful because most users do not want to build from source.
- A one-time paid official build creates less support pressure than a subscription.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Private source and paid EXE only | Harder trust problem for a not-yet-audited vault app |
| Donation-only open source | Unpredictable income and weak sustainability |
| Noncommercial source-available license | Not truly open source and can create adoption friction |
| Subscription-first model | Creates ongoing support/update expectations before the project is ready |

### Consequences

Positive:

- Improves trust and reviewability.
- Keeps a paid official-build path open.
- Avoids overpromising regular support.

Negative or tradeoffs:

- Others can build and redistribute compliant versions.
- Official build trust, signing, documentation, and distribution quality matter more.

### Review Trigger

Revisit before public release if the commercial plan changes, if a store imposes incompatible licensing terms, or if the release/license strategy changes.

## D-0009 - First-use security acknowledgement

- Status: Accepted
- Date: 2026-05-31
- Owner: Project owner
- Related documents: `TERMS.md`, `PRIVACY.md`, `DISCLAIMER.md`, `SECURITY.md`, `README.md`

### Context

ShellKrypt has irreversible security limits: no password recovery, plaintext export risk, clipboard limits, app-memory exposure while unlocked, and no external audit yet. These limits should be visible before a user creates, imports, or opens a vault.

### Decision

The desktop launcher requires a security acknowledgement before creating, importing, or opening a vault. Acceptance is stored in local app settings as a UTC timestamp plus the accepted acknowledgement version.

### Rationale

- Makes the no-recovery and local-only model explicit before vault use.
- Keeps the warning close to the moment of risk without repeating it on every launch.
- Allows material terms, privacy, disclaimer, or security text changes to require re-acceptance by bumping `AppSettings.CurrentSecurityAcknowledgementVersion`.
- Preserves the existing settings storage shape by adding an optional field.

### Alternatives Considered

| Alternative | Why Not |
|---|---|
| Show warnings only in documentation | Users may never read the docs before creating a vault |
| Show the acknowledgement every launch | Creates warning fatigue |
| Require acceptance before showing the launcher | Blocks harmless reading of the vault list and status |

### Consequences

Positive:

- Clearer consent before sensitive flows.
- Better alignment between product behavior, docs, and commercial release expectations.

Negative or tradeoffs:

- One more step for first-time users.
- The acknowledgement text and version must be updated whenever material security/privacy behavior changes.

### Review Trigger

Revisit before public 1.0 or when cloud sync, accounts, telemetry, biometric unlock, or mobile distribution changes the privacy/security model.

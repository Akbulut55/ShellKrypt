# AGENTS.md

## Role

This repository develops `ShellKrypt`, a local-only encrypted vault for desktop first and mobile later.

Every change must be scoped, testable, and consistent with the ownership boundaries in this document.

## Documentation Order

When documents disagree, use this priority:

1. `handbook/IDEA.md` for product intent and non-goals.
2. `SECURITY.md` for data, secret, auth, and safety rules.
3. `TERMS.md`, `PRIVACY.md`, and `DISCLAIMER.md` for user-facing legal/privacy limits.
4. `handbook/TECH_STACK.md` for approved tooling and runtime choices.
5. `handbook/DATABASE.md` for schema, migration, and persistence rules.
6. `handbook/PLAN.md` for engineering execution.
7. `handbook/ROADMAP.md` for sequencing and milestones.
8. `handbook/DEVELOPMENT.md` for local workflow.
9. `handbook/OPERATIONS.md` for release, rollback, and production operations.
10. `handbook/DECISIONS.md` for historical decisions and tradeoffs.

If a conflict is material, update the relevant document instead of silently working around it.

## Development Rules

- Inspect the repo before changing anything.
- Run `git status --short` before edits.
- Use `rg` for searching.
- Use `apply_patch` for manual edits.
- Preserve existing working product flows.
- Do not revert unrelated user changes.
- Keep generated `bin/`, `obj/`, `artifacts*/`, and `publish/` output uncommitted.
- Do not commit real user data, secrets, API keys, provider tokens, private logs, local vaults, backups, plaintext exports, screenshots containing secrets, or PII.
- Documentation-only changes do not require tests, but documentation should be checked for consistency.

## Ownership Boundaries

| Directory | Responsibility |
|---|---|
| `ShellKrypt.Core` | Domain models, payload records, service interfaces, security settings, transfer models |
| `ShellKrypt.Application` | Shared use-cases, settings, vault registry, activity, audit dismissal, item summaries, filters, pagination |
| `ShellKrypt.Infrastructure` | SQLite, crypto, backup/restore/import/export, file-backed stores, path guards |
| `ShellKrypt.UI.Shared` | Shared theme resources, reusable controls, converters, visual primitives |
| `ShellKrypt.Desktop` | Avalonia desktop shell, views, viewmodels, dialogs, desktop platform services |
| `ShellKrypt.Mobile` | Shared mobile shell and mobile viewmodels |
| `ShellKrypt.Mobile.Android` | Android app head and package metadata |
| `ShellKrypt.Mobile.iOS` | iOS app head and package metadata |
| `ShellKrypt.Tests` | xUnit tests |
| `handbook` | Product, planning, technical, operations, and decision documents |

Rules:

- UI logic does not belong in `ShellKrypt.Core`.
- Platform-specific UI/window/file picker/clipboard behavior stays in Desktop or mobile platform heads/adapters.
- Shared item/session/settings logic should move to `ShellKrypt.Application` when both desktop and mobile can use it.
- SQLite, crypto, import/export, and path guards stay in `ShellKrypt.Infrastructure`.
- Test fixtures must be synthetic.

## Versioning And Changelog

`CHANGELOG.md` follows Keep a Changelog.

- New changes first accumulate under `## [Unreleased]`.
- Before a release, move relevant entries into a versioned release section.
- When the user says `commit`, first move the relevant `Unreleased` entries for the work being committed into a new versioned section and update app version metadata when the committed work represents that version.
- Do not leave meaningful committed changes under `Unreleased` unless the user explicitly asks for a WIP commit without a version bump.
- Documentation-only changes can stay under `Changed`.
- User-visible product behavior usually increments minor before 1.0.
- Breaking vault format, API, or product-contract changes require explicit migration/release notes.

Release heading format:

```text
## [ShellKrypt X.Y.Z] - YYYY-MM-DD
```

Allowed subsection headings:

```text
### Added
### Changed
### Fixed
### Removed
### Security
```

## Commit Message Format

When the user asks for a commit, use a clear imperative summary. Add a body when the commit has multiple meaningful parts.

Rules:

- The first line is imperative and specific.
- Body bullets describe real changes when needed.
- Update and version `CHANGELOG.md` before committing meaningful changes.
- Push only when the user explicitly asks.

## Standard Commands

| Command | Purpose |
|---|---|
| `dotnet build .\ShellKrypt.slnx` | Build canonical solution |
| `dotnet test .\ShellKrypt.slnx` | Run all tests |
| `dotnet run --project .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj` | Run desktop app |
| `dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android` | Build Android app head |
| `dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive` | Check dependency vulnerabilities |

## Environment And Secrets

- No environment variables are required for normal desktop use.
- `SHELLKRYPT_APPROOT` can override local app-data location for development/tests.
- Secrets must never be committed.
- `.env` files are not committed.
- Logs must not include passwords, tokens, OTP seeds, card numbers, CVCs, API secret values, note contents, private exports, or raw vault payloads.

## Test Writing Instructions

After meaningful code changes, run relevant checks:

```powershell
dotnet build .\ShellKrypt.slnx
dotnet test .\ShellKrypt.slnx
```

When mobile shared code or UI.Shared changes:

```powershell
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

Add focused tests for:

- crypto tamper/wrong-key/truncated blob behavior
- vault unlock/change-password behavior
- settings/session normalization
- item search/filter/sort/pagination
- import/export validation and transactions
- path guard and deletion safety
- activity log sanitization

## Developer Workflow

```text
1. Read `handbook/IDEA.md`.
2. Read `handbook/PLAN.md`.
3. Read `SECURITY.md` when the change touches data, storage, logs, clipboard, imports, exports, crypto, or deletion.
4. Read `handbook/DATABASE.md` when the change touches schema, migrations, vault files, imports, backups, or retention.
5. Read `handbook/OPERATIONS.md` when the change touches release, signing, packaging, rollback, or support behavior.
6. Identify the smallest vertical slice.
7. Keep edits inside the responsible project boundary.
8. Add focused tests for changed behavior.
9. Run relevant build/test commands.
10. Update CHANGELOG.md when a meaningful change is complete.
11. Commit only when the user explicitly asks.
12. Push only when the user explicitly asks.
```

## Invariants

1. ShellKrypt is local-only by default.
2. There is intentionally no password recovery.
3. Sensitive item payloads are encrypted before storage.
4. Activity logs are vault-scoped and encrypted.
5. Plaintext exports are explicitly decrypted reports and must be warned.
6. Clipboard clearing is best-effort and not a security boundary.
7. No secrets, PII, real vaults, backups, or plaintext exports in commits.
8. Default `ShellKrypt.slnx` builds must remain usable on Windows without optional iOS tooling.
9. First-use security acknowledgement must stay accurate with `SECURITY.md`, `TERMS.md`, `PRIVACY.md`, and `DISCLAIMER.md`.

## UI Design And Development

- Preserve the existing dark ShellKrypt design language unless explicitly changing theme work.
- Use shared palette resources and shared controls where practical.
- Keep item table/filter/pagination patterns consistent across comparable screens.
- Use `ModalShell` for desktop item modals that share common structure.
- Mobile should use list cards and full-screen pages instead of desktop tables/modals.
- Critical controls must remain reachable on small desktop and mobile screens.

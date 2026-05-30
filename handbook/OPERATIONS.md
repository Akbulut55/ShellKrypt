# ShellKrypt: Operations

This document explains how ShellKrypt is released, rolled back, monitored, and operated after it has real users or production-like distribution. ShellKrypt is local-only today, so operations are mostly release, packaging, support, and incident response rather than server uptime.

## 1. Operational Summary

- Production owner: project owner
- Deployment target: local desktop and mobile app packages
- Production URL: none today
- Status page: none today
- On-call model: no formal rotation
- Support channel: not finalized
- Backend services: none

## 2. Environments

| Environment | Purpose | URL | Data Sensitivity | Access |
|---|---|---|---|---|
| Local | Development and manual testing | none | Synthetic or developer-owned local vaults | Developer |
| Private pre-release | Installer/package validation | none | Tester-owned local vaults | Project owner/testers |
| Public release | Future commercial distribution | Store or download page not finalized | Real user local data | Users |

Rules:

- Production user vault data is never collected by ShellKrypt by default.
- Testers must use synthetic or personally owned data and understand no-password-recovery behavior.
- Preview/release packages must not include private vaults, backups, exports, or local logs.

## 3. Release Build

Windows self-contained build:

```powershell
dotnet publish .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

Android build:

```powershell
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

iOS build:

```powershell
dotnet build .\ShellKrypt.Mobile.iOS\ShellKrypt.Mobile.iOS.csproj -f net10.0-ios
```

Release approval: project owner.

Pre-release checklist:

- [ ] `dotnet build .\ShellKrypt.slnx` passes.
- [ ] `dotnet test .\ShellKrypt.slnx` passes.
- [ ] Dependency vulnerability check is clean or findings are triaged.
- [ ] README, SECURITY, TERMS, PRIVACY, DISCLAIMER, CHANGELOG, and release notes are current.
- [ ] No generated outputs, vaults, backups, plaintext exports, or private logs are staged.
- [ ] Windows smoke test passes.
- [ ] First-use security acknowledgement, plaintext export warning, and no-password-recovery warning are visible.
- [ ] Security acknowledgement version was bumped if the acknowledgement, terms, privacy, disclaimer, or security text materially changed.
- [ ] Code signing, installer, terms/privacy/disclaimer docs, and support channel are ready for public/commercial release.

## 4. Rollback

Rollback strategy:

> Revert to the previous known-good app package. If vault format changes are introduced, rollback must account for whether the previous app can still read the vault.

Rollback rules:

- Prefer backward-compatible vault format changes.
- Avoid destructive vault migrations.
- Require explicit backup guidance before shipping format changes.
- Keep the previous release artifact available until the new release is validated.

Rollback command:

```powershell
git revert <commit>
dotnet build .\ShellKrypt.slnx
dotnet test .\ShellKrypt.slnx
```

## 5. Monitoring And Alerts

ShellKrypt has no server monitoring today.

| Signal | Tool | Alert Threshold | Owner |
|---|---|---|---|
| Build health | Local/CI build | Build failure | Project owner |
| Test health | xUnit | Test failure | Project owner |
| Dependency vulnerabilities | `dotnet list package --vulnerable` | New vulnerable package | Project owner |
| User-reported data issue | Support channel not finalized | Any potential data loss/security report | Project owner |

## 6. Logs

Log locations:

- Vault-scoped activity logs are encrypted inside the active `.skvault`.
- App metadata is under `%APPDATA%\ShellKrypt` by default.
- There is no server log collection.

Rules:

- Logs must follow `SECURITY.md`.
- Do not log secrets, tokens, private files, raw PII, OTP seeds, card numbers, CVCs, passwords, API secret values, or note contents.
- Activity reports are plaintext exports and must be treated as sensitive.

## 7. Backups And Restore

- Backup owner: vault owner.
- Backup frequency: user-managed; recommended before master-password changes, app upgrades, or moving vaults.
- Backup storage: user-selected `.skbx` location.
- Retention period: user-managed.
- Restore test frequency: before relying on a vault for important records.

Restore path:

```text
Settings
  -> Backup and Restore
  -> select .skbx
  -> enter backup passphrase
  -> preview/import
```

Rules:

- Encrypted backups require a separate backup passphrase.
- ShellKrypt cannot restore without the backup passphrase.
- Plaintext exports are not backups unless the user intentionally accepts decrypted storage risk.

## 8. Incident Response

Severity levels:

| Severity | Meaning | Response |
|---|---|---|
| SEV1 | Potential data loss, vault corruption, secret leakage, or crypto flaw | Stop release, preserve evidence, triage, patch, document |
| SEV2 | Major workflow broken such as unlock, backup/restore, item save, or delete safety | Patch and run regression checklist |
| SEV3 | Minor UI or documentation issue | Fix in normal cycle |

Incident checklist:

- [ ] Confirm affected version and workflow.
- [ ] Preserve useful logs without exposing sensitive data.
- [ ] Determine whether user vault data, backups, or plaintext exports are at risk.
- [ ] Mitigate or roll back.
- [ ] Update `SECURITY.md`, `CHANGELOG.md`, and release notes if relevant.
- [ ] Add regression tests or smoke-test steps.

## 9. Production Access

| System | Access Method | Who Can Access | Review Frequency |
|---|---|---|---|
| Source repository | Git hosting | Project owner | Per release |
| Release artifacts | Local or store pipeline, not finalized | Project owner | Per release |
| Signing keys | Secure local or platform keystore, not finalized | Project owner | Per release |
| User vaults | Local user devices only | User only | Not accessible by project |

Rules:

- No shared production accounts.
- Signing keys must not be committed.
- User vaults are not collected for debugging unless the user explicitly provides a synthetic or redacted file.

## 10. Operational Runbooks

| Runbook | When To Use | Link Or Command |
|---|---|---|
| Build release | Preparing a Windows package | `dotnet publish .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64` |
| Run full checks | Before commit/release | `dotnet build .\ShellKrypt.slnx`; `dotnet test .\ShellKrypt.slnx` |
| Check dependencies | Before release | `dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive` |
| Restore backup | User restore workflow | Settings backup/restore flow |
| Rotate exposed user secret | User action after exposure | Edit affected item or change master password, then create new backup |
| Remove generated outputs | Before commit | Ensure `bin/`, `obj/`, `publish/`, and `artifacts*/` are unstaged/uncommitted |

# ShellKrypt: Security

This document defines ShellKrypt's security, privacy, data-handling, and secret-management rules. Any implementation that touches vaults, credentials, storage, imports, exports, logs, clipboard, or release packaging must comply with this document.

## 1. Security Summary

- Data sensitivity: high
- Auth required: local vault unlock with master password
- External providers: none by default
- Stores user data: yes, in local `.skvault` files
- Stores regulated data: can store payment card details if the user enters them; ShellKrypt is not a PCI-certified product
- Distribution target: local desktop app first; mobile app heads are in progress
- Audit status: not externally audited
- License status: prepared for GPL-3.0-or-later source release

Security claims must stay factual:

- local-only encrypted vault
- AES-GCM encrypted payloads
- Argon2id master password derivation
- no password recovery
- not externally audited yet

Do not use claims such as "military-grade", "unhackable", or "zero risk".

## 2. Reporting A Vulnerability

Report suspected vulnerabilities through the repository security contact when one is configured. Until then, report suspected vulnerabilities privately to the project owner.

When reporting, include:

- affected ShellKrypt version or commit
- operating system and platform
- whether the vault was locked or unlocked
- minimal reproduction steps using synthetic data
- expected behavior and actual behavior
- whether any real secrets, vaults, backups, exports, or logs may be involved

Do not send real vault files, master passwords, backup passphrases, API keys, OTP seeds, card numbers, CVCs, plaintext exports, or private logs unless explicitly requested and sanitized.

Expected handling:

- Acknowledge the report when received.
- Reproduce with synthetic data.
- Assess impact against this security policy.
- Patch privately when needed before publishing enough detail for users to update.
- Add regression tests where practical.
- Publish release notes without exposing exploit details before users can update.

Before broad public distribution, configure a dedicated security contact such as a private email address or GitHub Security Advisories workflow.

## 3. Supported Versions

ShellKrypt is currently pre-1.0. Only the latest distributed build is expected to receive fixes unless a release note says otherwise.

When stable releases are published, maintain this table:

| Version | Supported |
|---|---:|
| latest stable | Yes |
| older pre-1.0 builds | No |

## 4. Data Classification

| Data Type | Examples | Storage Allowed | Logging Allowed | Notes |
|---|---|---:|---:|---|
| Public project data | README, docs, synthetic examples | Yes | Yes | Must not include real secrets |
| App metadata | settings, vault registry, audit dismissals, Backup Center history, automatic-backup status, Emergency Kit checklist state | Yes | Redacted | Must not contain raw item secrets |
| Private user data | titles, usernames, emails, URLs, notes, card metadata | Restricted | Redacted only | Store sensitive payloads encrypted |
| Secrets | passwords, API keys, tokens, OTP seeds, CVCs, backup passphrases, master passwords | Encrypted only where required | No | Never commit or log |
| Plaintext exports | decrypted JSON reports, activity reports | User-controlled only | No | Must be clearly labeled and warned |
| Vault files | `.skvault` SQLite databases | Local file only | Path basename only when practical | Sensitive; do not commit |
| Backups | `.skbx` encrypted backup packages | User-controlled only | Basename only | Requires separate backup passphrase |

## 5. Secret Handling

- Secrets must never be committed.
- `.env` files are local only and are not required for normal desktop use.
- Local vaults, backups, plaintext exports, screenshots containing secrets, and private logs must not be committed.
- Master passwords and backup passphrases must not be persisted by ShellKrypt.
- Automatic-backup passphrases are session-only memory values and must be cleared on lock, vault switch, import/restore, and app close.
- App settings, vault registry, audit dismissal state, and activity logs must not contain raw passwords, card numbers, CVCs, OTP seeds, API secret values, or note contents.
- Clipboard contents are outside ShellKrypt's full control; clearing is best-effort and is not a security boundary.
- Rotate any real credentials after suspected exposure.

Approved secret locations:

| Secret | Scope | Storage Location | Rotation Policy |
|---|---|---|---|
| Master password | Vault unlock | User memory only; not stored | User changes by changing master password |
| Vault key | Active vault | Encrypted in `vault_meta.encryptedVaultKey`, present in memory while unlocked | Rewrapped on master-password change |
| Item secrets | Vault items | AES-GCM encrypted payloads in `.skvault` | User edits/replaces records |
| Backup passphrase | `.skbx` backup and automatic backup session | User memory/session memory only; not stored | User creates a new backup |

## 6. Authentication And Authorization

Auth model:

> Local vault unlock. The user proves knowledge of the master password, ShellKrypt derives key material with Argon2id, decrypts the vault key, and keeps the vault unlocked until lock, timeout, focus-loss policy, vault switch, or app exit.

Roles:

| Role | Capabilities | Restrictions |
|---|---|---|
| Vault owner | Full local access after successful unlock | Must know master password or backup passphrase |
| Locked local user | Can see launcher/unlock UI | Cannot decrypt vault records |
| App maintainer | Can change app code and release builds | Cannot recover user vaults without password/passphrase |

Rules:

- There is no server-side account or remote authorization layer.
- Privileged local actions include unlock, export, import, restore, delete vault, change master password, reveal secret, and copy secret.
- Destructive or plaintext-producing actions require clear confirmation.
- Lock clears in-memory session state as much as practical and clears clipboard through best-effort OS clipboard APIs.

## 7. Threat Model

| Threat | Impact | Mitigation | Verification |
|---|---|---|---|
| Forgotten master password | Permanent data loss | No-recovery warnings, backup guidance, change-password flow | Manual smoke test and docs review |
| Offline vault theft | Secret disclosure attempt | Argon2id key derivation, AES-GCM encrypted payloads, encrypted vault key | Crypto tests, unlock tests, wrong-key tests |
| Tampered ciphertext or swapped payload | Corrupt or malicious data | AES-GCM tag validation and associated data where practical | Tamper/swap/truncation tests |
| Plaintext export mishandling | Full decrypted disclosure | Explicit confirmation, filename warning, post-export warning | UI/manual test and activity log review |
| Clipboard exposure | Secret copied outside app | Copy-disabled setting, timeout clearing, clear on lock/switch/destructive flows | Clipboard setting tests/manual checks |
| Automatic backup misconfiguration | Missing recovery copy or unexpected local backup files | Disabled by default, session-only passphrase, verification after export, retention limited to exact auto-backup filename pattern | Scheduler and retention tests/manual checks |
| Activity log leakage | Secrets in event history | Vault-scoped encrypted logs and redaction rules | Activity log tests and code review |
| Unsafe file deletion/overwrite | User data loss | Path canonicalization, extension guards, active-vault overwrite checks | File guard tests |
| Malformed import package | Crash, partial import, resource exhaustion | Size limits, validation before KDF work, transactions | Import parser tests |
| App memory exposure while unlocked | Secret disclosure to local malware/debuggers | Clear lock behavior and honest documentation | Threat-model review |
| Supply-chain vulnerability | Compromise through dependencies | Vulnerability checks and dependency review | `dotnet list ... package --vulnerable --include-transitive` |

## 8. Input And Output Safety

- Validate paths before reading, writing, deleting, importing, or exporting.
- Enforce `.skvault`, `.skbx`, `.json`, and `.csv` extensions where expected.
- Enforce import size and row/field limits.
- Validate backup package fields before expensive Argon2 work.
- Use transactions for imports/restores that can modify many records.
- Treat plaintext JSON exports and activity report exports as decrypted reports.
- Treat printable Emergency Kit exports as plaintext safe-metadata reports. They must not contain passwords, backup passphrases, hints, item secrets, card numbers, OTP seeds, or note contents.
- Avoid full filesystem paths in activity log details when the basename is sufficient.
- Keep the first-use security acknowledgement accurate when no-recovery, export, clipboard, audit, or privacy behavior changes.
- Bump `AppSettings.CurrentSecurityAcknowledgementVersion` when material terms, privacy, disclaimer, or security text changes should require users to accept again.

## 9. Logging And Observability

Allowed:

- High-level events such as vault created, vault unlocked, backup exported, import completed, activity logs cleared.
- Backup Center and Emergency Kit events when they contain only safe operation names, basenames, counts, statuses, and timestamps.
- Synthetic IDs, session IDs, timestamps, categories, and statuses.
- File basenames for imports/exports when useful.
- Redacted or summarized metadata.

Not allowed:

- Master passwords, backup passphrases, item passwords, API keys, tokens, OTP seeds, CVCs, full card numbers, or note contents.
- Raw QR payloads containing OTP secrets.
- Full plaintext exports or decrypted backup contents.
- Full local paths when basename is enough.
- Real user data in test fixtures or committed logs.

## 10. Data Retention And Deletion

- Retention policy: local user controls vault files, manual backups, and exports.
- Activity log retention: encrypted per-vault log store keeps a bounded recent history.
- Deletion policy: deleting a vault removes the selected `.skvault` target after safety checks; backups/exports remain user-managed.
- Export policy: encrypted backups are preferred; plaintext exports are allowed with explicit confirmation and warnings.
- Backup policy: users should create and verify `.skbx` backups before relying on a vault.
- Automatic-backup policy: in-app automatic backups run only while the app is open, the vault is unlocked, a backup directory is configured, and a session-only backup passphrase is available. Retention cleanup must delete only exact ShellKrypt auto-backup filenames.

Rules:

- Backups have the same sensitivity assumptions as primary storage.
- Plaintext reports are more sensitive than encrypted vault files and should be deleted when no longer needed.
- Generated exports and backups must not be committed.

## 11. Dependency And Supply Chain Policy

- Add dependencies only when they reduce real complexity.
- Prefer maintained packages with clear licenses.
- Review packages that run native code, parse untrusted files, or affect crypto/storage.
- Run vulnerability checks before release:

```powershell
dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive
```

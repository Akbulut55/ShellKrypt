# ShellKrypt: Security

This document records ShellKrypt's public security expectations: what must be protected, who can access it, what must not be exposed, and how security risks are reviewed.

Related documents:

- Privacy notice: [`PRIVACY.md`](PRIVACY.md).
- Disclaimer: [`DISCLAIMER.md`](DISCLAIMER.md).

Detailed architecture, threat-model, cryptography, data-handling, format, and
development documents are maintained privately during pre-1.0 development. This
public policy defines the security expectations users and vulnerability
reporters can rely on.

## Security Summary

- Security owner: Project owner and maintainer.
- Security status: active and needs external review.
- Data sensitivity: high.
- Authentication required: yes, through local master-password vault unlock.
- Authorization model: local vault ownership after successful unlock; no remote roles or account service.
- External systems with security impact: operating system, filesystem, clipboard, package supply chain, optional mobile platforms, and user-selected backup or synchronization tools.
- Highest-risk area: loss or disclosure of vault key material, decrypted secrets, master passwords, backup passphrases, or plaintext exports.

ShellKrypt is pre-1.0 and has not received an external security audit. Report suspected vulnerabilities privately to the project owner or through the repository's private security-advisory channel when available. Do not include real vaults, credentials, backups, exports, or private logs in a report; reproduce issues with synthetic data.

Only the latest distributed pre-1.0 build is expected to receive security fixes unless release notes say otherwise. Security claims must remain factual: local-only encrypted vault, Argon2id password derivation, AES-GCM encrypted payloads, no password recovery, and no external audit.

## Protected Assets

| Asset | Why It Matters | Protection Needed |
|---|---|---|
| Vault key and unlock material | Controls access to encrypted vault records | Authenticated encryption, memory-limited lifetime, and no logging |
| Item secrets and private records | Includes passwords, tokens, OTP seeds, card data, notes, and project variables | Encrypt before persistence and reveal only during explicit unlocked workflows |
| Vaults, backups, and exports | Can contain complete or partial user data | Path validation, explicit user actions, clear encrypted/plaintext distinction, and safe deletion rules |
| Activity history and app metadata | Can expose behavior, paths, names, and security posture | Encrypt sensitive activity details, minimize retained metadata, and sanitize output |

Protection rules:

- Sensitive item payloads must be encrypted before storage, and cryptographic failures must fail closed.
- Master passwords and backup passphrases must not be persisted by ShellKrypt.
- No security control or status indicator may imply password recovery or protection against a fully compromised device.

## Sensitive Data Rules

| Data Or Material | Allowed Location | Logging Allowed | Sharing Allowed | Notes |
|---|---|---:|---:|---|
| Passwords, tokens, OTP seeds, card secrets, and note contents | Encrypted vault payload or temporary unlocked memory | No | Restricted to explicit user actions | Never commit or include in fixtures |
| Vault and backup files | User-controlled local files | Basename or safe status only | Restricted | `.skvault` and `.skbx` remain sensitive |
| Plaintext exports and activity reports | Explicit user-selected output path | No contents | User controlled | Decrypted output requires warning |
| App settings and vault registry | Local app-data metadata | Redacted | No automatic sharing | Must not contain raw item secrets |

Sensitive data rules:

- Use only synthetic records in tests, screenshots, issues, and shared logs.
- Do not duplicate referenced API keys or other secrets unless the user explicitly requests an independent copy.
- Treat clipboard contents, plaintext exports, process memory, and files handled by other applications as outside the encrypted-at-rest boundary.

## Secrets And Credentials

| Secret Or Credential | Used For | Stored In | Rotation Or Review |
|---|---|---|---|
| Master password | Unlocking wrapped vault-key material | User knowledge and temporary input only | User-controlled password change |
| Vault key | Encrypting and decrypting vault payloads | Wrapped in vault metadata and temporarily in memory while unlocked | Rewrapped on master-password change; format changes require review |
| Backup passphrase | Protecting `.skbx` backups | User knowledge or current in-app session memory only | Create a new backup when changed |
| Signing and distribution credentials | Future official build verification | Approved external secure storage, never the repository | Review before each release and rotate after suspected exposure |

Secret rules:

- Never commit secrets, `.env` values, real vaults, backups, plaintext exports, or signing material.
- Clear session-only secret state on lock, vault switch, restore/import transitions, and application exit where practical.
- Rotate any credential that may have been exposed and avoid sharing raw evidence in public reports.

## Access Control

| Actor, Role, Or Process | Can Access | Must Not Access | Notes |
|---|---|---|---|
| Unlocked vault owner | Decrypted records required by explicit actions | Unrelated files or records outside the selected vault | Must successfully unlock locally |
| Locked local user | Launcher, vault metadata needed for selection, and unlock form | Decrypted item payloads and vault key | No remote authorization fallback |
| ShellKrypt maintainer | Source code and synthetic development data | User vaults, passwords, passphrases, or recovery material | Cannot recover a user's encrypted vault |
| Backup or export workflow | Validated data for the requested operation | Unrelated filesystem paths and unrequested secrets | Requires explicit user action and warnings where plaintext is produced |

Access rules:

- Privileged actions include unlock, reveal, copy, export, restore, import, change password, and deletion.
- Destructive or plaintext-producing actions require clear confirmation and path validation.
- Platform-specific services receive only the minimum data needed for the active action.

## Input, Output, And Boundary Rules

| Boundary | Main Risk | Rule |
|---|---|---|
| Vault, backup, import, and export files | Traversal, overwrite, malformed input, data loss, or disclosure | Canonicalize paths, enforce expected formats and limits, validate before mutation, and use transactions where needed |
| Clipboard, reveal, Quick Fill, and Auto-Type | Secret disclosure to other processes or the wrong target | Require explicit action, respect copy controls, clear best-effort, and never claim unsupported platform behavior |
| Project Secrets scanning and `.env` workflows | Reading excessive files or exposing plaintext values | Scan only user-selected roots with limits and ignores; never log or display matched secret values |
| QR, image, CSV, JSON, and package parsers | Resource exhaustion, malformed data, or partial writes | Limit input size, validate structure, and fail without unsafe partial state |

Boundary rules:

- Enforce expected extensions and file-size, row-count, field-size, and scan limits before expensive work.
- Treat all imported content and external-system responses as untrusted.
- Do not implement Project Secrets command execution, shell injection, or automatic process-variable injection.

## Logging And Error Handling

Allowed:

- Safe action names, synthetic or opaque IDs, timestamps, categories, statuses, counts, and basenames.
- Redacted metadata and user-facing errors that identify the failed operation without exposing payload contents.
- Security-audit findings that name affected record metadata or variable keys without including secret values.

Not allowed:

- Master passwords, backup passphrases, passwords, API keys, tokens, OTP seeds, CVCs, full card numbers, or Project Secret values.
- Note contents, raw QR payloads, decrypted payloads, full exports, hardcoded secret matches, or backup contents.
- Full local paths when a basename or safe description is sufficient.

## External Code And Dependency Rules

Rules:

- Add dependencies only when they remove meaningful complexity and have maintainable source, licensing, and security posture.
- Review packages that affect cryptography, storage, native code, parsing, platform integration, or distribution more strictly.
- Run dependency vulnerability checks before release and do not ignore applicable advisories without a documented assessment.

Review triggers:

- A cryptographic, storage, parsing, native, or platform dependency receives a security advisory or major-version update.
- A feature adds networking, accounts, telemetry, remote services, plugins, command execution, browser integration, or new secret-sharing boundaries.

## Open Security Questions

- Which private reporting address and response expectations will be published before public 1.0?
- What external security review scope is required before recommending ShellKrypt for real sensitive data?
- Which signing, update-verification, and release-provenance controls will protect official builds?

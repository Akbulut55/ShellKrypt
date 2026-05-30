# ShellKrypt

ShellKrypt is a local-only encrypted vault for users who want to manage sensitive records on their own devices without a cloud account or remote recovery service.

This is the canonical product document for the project. It explains what the product is, why it exists, who it serves, what it will not do, and which product assumptions must stay true while the implementation evolves.

## 1. Product Thesis

Many people need a small private vault for credentials, payment cards, API keys, authenticator seeds, and private notes, but do not want those records synced through a vendor account. ShellKrypt should provide the core benefits of a password-manager-style vault while keeping storage local, transparent, and recoverable only through user-controlled backups.

Correct product definition:

> ShellKrypt is a local-only encrypted desktop vault that stores sensitive records in user-controlled `.skvault` files protected by a master password and optional encrypted backups.

## 2. Target Users

Primary users:

- Individual developers and technical users who manage API keys, web logins, cards, authenticator seeds, and private notes.
- Privacy-conscious users who prefer a local encrypted vault over a cloud account.

Secondary users or operators:

- The project owner, who currently develops, releases, and supports the app.
- Future testers who validate Windows desktop behavior and mobile readiness.

Users this project does not optimize for:

- Enterprise teams requiring centralized administration, account recovery, SSO, shared vaults, or compliance dashboards.
- Users who expect cloud sync, web access, browser autofill, or automatic password recovery.

## 3. Problems

### 3.1 User Problems

- Sensitive records are often scattered across browsers, notes, chat history, plain files, and memory.
- Cloud password managers introduce account, sync, breach, and trust concerns that some users do not want.
- Developers need a flexible place for API keys and provider-specific secret fields that do not fit simple login/card models.
- Users need explicit warnings around decrypted exports and master-password loss.

### 3.2 Operator Or Business Problems

- The app must be honest about security claims because it is not externally audited.
- Release readiness depends on installation, signing, terms/privacy/disclaimer docs, support, and platform validation, not only feature count.
- Mobile support should reuse the same product logic without forcing separate Android and iOS product shells.

### 3.3 Data, Regulation, Or Knowledge Freshness

ShellKrypt stores user-entered secrets and private records. The product itself does not rely on live pricing, legal, or regulatory feeds, but release and distribution decisions can depend on current platform rules, code-signing requirements, encryption export compliance, and store policies. Those external requirements must be verified before public distribution.

## 4. Product Promise

The product should help users:

- Store sensitive records locally in encrypted vault files.
- Find, copy, audit, back up, and manage those records without a cloud account.
- Understand that there is no password recovery and that plaintext exports are sensitive.

The product is successful when:

- A user can create, unlock, use, back up, restore, and delete a vault confidently.
- The common item types behave consistently across search, filtering, details, edit, delete, copy, and pagination.
- Security warnings are clear without making exaggerated claims such as "unhackable" or "zero risk".

## 5. Core Product Mechanics

### Mechanic 1 - Local Encrypted Vault

```text
User creates or opens a vault
  -> app derives unlock material from the master password
  -> app decrypts the vault key
  -> app encrypts/decrypts item payloads locally
  -> app stores only encrypted sensitive payloads in the vault database
```

### Mechanic 2 - Item Workspaces

ShellKrypt organizes records by item type:

- Web logins
- Credit cards
- API keys
- Authenticator entries
- Markdown notes
- Activity logs

Each workspace can have a different editor, but shared search, filtering, pagination, modal, theme, and summary logic should stay consistent.

### Mechanic 3 - Security-Aware Actions

Sensitive actions require stronger UX:

- Plaintext export requires explicit confirmation and warning.
- Vault deletion must confirm the selected `.skvault` target.
- Copy actions can be disabled and clipboard clearing is best-effort only.
- Master-password loss cannot be recovered unless the user has a valid backup and backup passphrase.

## 6. Actors And Permissions

| Actor | Description | Product Permissions |
|---|---|---|
| Vault owner | Local user who knows the master password | Create, unlock, edit, export, import, delete, and back up local vaults |
| Locked user | Local user without a valid master password | Cannot decrypt or recover vault contents |
| App process | Local desktop or mobile runtime | Can access decrypted values only while the vault is unlocked |
| Project maintainer | Developer/releaser | Can ship app changes but cannot recover user vaults |

## 7. Core Objects

| Object | Meaning | Created By | Consumed By |
|---|---|---|---|
| Vault | Local `.skvault` SQLite database containing metadata, encrypted records, labels, and encrypted activity logs | Vault owner | Desktop/mobile app |
| Vault key | Random key encrypted by master-password-derived key material | App during vault creation | App while unlocked |
| Item payload | Sensitive JSON payload encrypted with AES-GCM | App item editors | Item detail/list/audit workflows |
| Encrypted backup | `.skbx` export protected by a separate backup passphrase | Vault owner | Restore/import workflow |
| Plaintext export | Decrypted JSON report | Vault owner | External review/manual handling |
| Activity log entry | Vault-scoped encrypted audit/event record | App | Activity Logs view and report export |

## 8. Primary User Journeys

### Journey 1 - Create And Use A Vault

1. User opens ShellKrypt and creates a vault.
2. User chooses a master password.
3. User adds records in Web Logins, Cards, API Keys, Authenticator, or Markdown Notes.
4. User locks the vault and later unlocks it with the same master password.

Expected result:

> Sensitive records are available only after unlock and are stored encrypted at rest.

### Journey 2 - Back Up And Restore

1. User creates an encrypted `.skbx` backup with a separate passphrase.
2. User stores the backup somewhere they control.
3. User restores or imports the backup into a vault.

Expected result:

> The user can move or recover vault contents without ShellKrypt account recovery or cloud sync.

### Journey 3 - Audit And Remediate

1. User runs Security Audit.
2. ShellKrypt identifies weak, reused, or stale web login passwords.
3. User routes to remediation and updates risky entries.

Expected result:

> The user can find and reduce risky credential patterns without exposing data to a remote service.

## 9. What It Does Not Do

- No cloud sync by default.
- No account system, server vault, or remote recovery.
- No guarantee that clipboard clearing fully removes OS clipboard history.
- No external security audit claim.
- No "military-grade", "unhackable", or "zero risk" security language.
- No enterprise team sharing, admin console, or SSO workflow in the current scope.

## 10. Constraints

- C1 - Sensitive payloads must be encrypted before storage.
- C2 - No secrets, credentials, private user data, real vaults, plaintext exports, or private logs in source control.
- C3 - The default desktop build must remain usable on Windows without optional mobile workloads.
- C4 - Activity logs must not contain raw passwords, card numbers, CVCs, OTP seeds, API secret values, or note contents.
- C5 - The product must clearly communicate no-password-recovery behavior.

## 11. Product Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| User forgets master password | Medium | High | Prominent no-recovery warning, backup guidance, change-password flow |
| User mishandles plaintext export | Medium | High | Strong confirmation, clear filename, post-export warning |
| Security claims exceed actual audit level | Medium | High | Use limited factual claims only and document not externally audited |
| Mobile UX copies desktop tables/modals poorly | Medium | Medium | Use mobile lists and full-screen pages |
| Platform release work is underestimated | Medium | Medium | Track code signing, installers, store assets, terms/privacy/disclaimer docs, and export compliance separately |

## 12. Open Product Questions

1. Which languages should be added before a broad public 1.0 release?
2. Should mobile ship as a companion app before desktop reaches 1.0, or wait until desktop is stable?
3. What paid/commercial model, support channel, and terms will be used?
4. Which desktop targets beyond Windows are worth validating first?

## 13. Related Planning Documents

- `README.md` - project entry point and quickstart.
- `handbook/PLAN.md` - engineering execution plan.
- `handbook/ROADMAP.md` - product milestones and sequencing.
- `handbook/TECH_STACK.md` - technical stack and tooling decisions.
- `handbook/DATABASE.md` - schema, migrations, persistence, and data ownership.
- `SECURITY.md` - data, threat, auth, and secret-handling rules.
- `handbook/DEVELOPMENT.md` - local setup and developer workflow.
- `handbook/OPERATIONS.md` - release, backup, rollback, and production runbooks.
- `handbook/DECISIONS.md` - durable decision log.
- `AGENTS.md` - repository instructions for coding agents.

# ShellKrypt: Database

This document defines persistence ownership, schema rules, migrations, seed data, backups, and data-retention expectations for ShellKrypt.

## 1. Persistence Summary

- Primary database: SQLite `.skvault` files
- Query layer: `Microsoft.Data.Sqlite`
- Migration tool: code-managed schema creation and targeted compatibility logic
- Local database strategy: user-controlled vault files under the ShellKrypt app-data directory or a user-selected path
- Test database strategy: temporary local files created by xUnit tests
- Production database owner: local vault owner

## 2. Data Ownership

| Data Area | Owner | Source Of Truth | Notes |
|---|---|---|---|
| Vault metadata | Infrastructure vault service | `vault_meta` table | KDF parameters, salt, encrypted vault key |
| Items | Item services and vault service | `items` table | Sensitive payload stored in `encryptedPayload` |
| Labels | Vault storage | `labels` and `item_labels` tables | Labels may include encrypted name compatibility field |
| Activity logs | Activity log service | `activity_logs` table | Encrypted per-vault log payloads |
| App settings | Application settings service | app-data `settings.json` | No raw secrets |
| Vault registry | Vault registry service | app-data `vaults.json` | Paths/display names, no raw secrets |
| Audit dismissals | Audit dismissal service | app-data `audit-dismissals.json` | Finding fingerprints, no item secrets |
| Backups | Vault transfer service | user-selected `.skbx` files | Encrypted with separate backup passphrase |

Rules:

- Each persisted domain object must have an owner.
- Sensitive data handling must follow `SECURITY.md`.
- App metadata files must never become a shortcut for storing item secrets.

## 3. Schema Conventions

- Primary keys: text IDs for items, labels, activity logs; singleton integer key for `vault_meta`.
- Timestamps: UTC text values such as `createdAtUtc`, `updatedAtUtc`, and `timestampUtc`.
- Soft delete policy: no soft delete today; deletes remove records from the local vault.
- Naming convention: existing schema uses camelCase column names.
- Foreign key policy: SQLite foreign keys enabled; `item_labels.itemId` and `item_labels.labelId` cascade on delete.
- Enum policy: `items.type` stores numeric `ItemType` values.

Required item columns:

- `id`
- `type`
- `favorite`
- `createdAtUtc`
- `updatedAtUtc`
- `encryptedPayload`

## 4. Current Vault Schema

| Table | Purpose | Notes |
|---|---|---|
| `vault_meta` | Vault format and unlock metadata | KDF parameters, salt, encrypted vault key |
| `items` | Encrypted domain records | Web logins, cards, notes, authenticators, API keys |
| `labels` | Label metadata | `encryptedName` exists for encrypted label compatibility; `name` supports current lookup |
| `item_labels` | Many-to-many item/label links | Cascades on item/label delete |
| `activity_logs` | Vault-scoped encrypted activity entries | Bounded recent history |

Indexes:

| Table | Index Or Constraint | Reason |
|---|---|---|
| `vault_meta` | `PRIMARY KEY CHECK (id = 1)` | Singleton metadata row |
| `items` | `PRIMARY KEY (id)` | Item lookup/update/delete |
| `labels` | `idx_labels_name` on `name COLLATE NOCASE` | Case-insensitive label uniqueness |
| `item_labels` | primary key `(itemId, labelId)` | Prevent duplicate links |
| `item_labels` | `idx_item_labels_itemId` | Item label lookup |
| `item_labels` | `idx_item_labels_labelId` | Label item lookup |
| `activity_logs` | `idx_activity_logs_timestampUtc` | Recent activity pagination/sort |

## 5. Migration Policy

- Schema creation is handled by infrastructure code when a vault is created/opened.
- Compatibility changes must be deterministic and covered by tests.
- Destructive schema changes require explicit approval and backup awareness.
- Vault format or crypto format changes must include migration notes and rollback limitations.
- Generated local database files must not be committed.

Migration commands:

```powershell
dotnet test .\ShellKrypt.Tests\ShellKrypt.Tests.csproj
```

There is no separate migration CLI today.

## 6. Access Rules

| Role Or Service | Read | Write | Notes |
|---|---|---|---|
| Locked app | Launcher metadata only | Vault registry/settings only | No item payload decryption |
| Unlocked app | Active vault records through services | Active vault records through services | Requires vault key in memory |
| Vault transfer service | Active vault snapshot | Backup/import/CSV operations | Uses path guards and transactions |
| Activity log service | Active vault logs | Active vault logs | Logs encrypted payloads only |

Rules:

- UI code should access persistence through application/infrastructure services, not direct SQL.
- Imports/restores that change multiple records should be transactional.
- Activity events should be sanitized before persistence.

## 7. Seed Data And Fixtures

- Seed command: none
- Fixture directory: none dedicated today
- Demo data policy: synthetic only
- Test data policy: synthetic only

Rules:

- Fixtures must not contain real user data.
- Test vaults should be created under temporary paths.
- Tests should not depend on production services or production data.

## 8. Retention, Deletion, And Audit

- Retention policy: vault owner controls local vault, backup, and export files.
- Deletion policy: vault deletion removes the selected `.skvault` after path safety checks.
- Activity event policy: encrypted, vault-scoped, bounded recent history.
- Backup retention: user controlled.

Rules:

- Private user data should have an explicit delete path through item delete or vault delete.
- Activity events must avoid raw secret payloads.
- Backups and plaintext exports are user-managed files and must be treated as sensitive.

## 9. Backup And Restore

- Encrypted backups use `.skbx`.
- Backup encryption uses a separate passphrase and AES-GCM through the shared blob helper.
- Plaintext JSON exports are decrypted reports and require explicit confirmation.
- CSV import is for supported login-style data and must validate rows, fields, and duplicate strategy.

Rules:

- Avoid overwriting the active vault during export/import.
- Validate package fields before expensive KDF work.
- Use transactions to avoid partially mixed imports.

## 10. Database Verification

Use these checks when schema or persistence behavior changes:

```powershell
dotnet build .\ShellKrypt.slnx
dotnet test .\ShellKrypt.slnx
```

Manual checks:

- Create and unlock a new vault.
- Add/edit/delete each item type.
- Export encrypted backup and restore it.
- Run plaintext export and confirm warning text.
- Load, filter, clear, and export activity logs.

# ShellKrypt: Development

This document explains how to run, test, and work on ShellKrypt locally. Product direction belongs in `handbook/IDEA.md`; technical choices belong in `handbook/TECH_STACK.md`; implementation sequencing belongs in `handbook/PLAN.md`.

## 1. Prerequisites

Required:

- .NET 10 SDK
- Windows for the primary tested desktop workflow
- Git

Optional:

- Android workload, Android SDK, and emulator/device for Android builds
- macOS, Xcode, Apple signing/provisioning, and .NET iOS workload for iOS builds
- SQLite inspection tool for manual vault debugging, used only with synthetic test vaults

## 2. Local Setup

Restore dependencies:

```powershell
dotnet restore .\ShellKrypt.slnx
```

Start desktop development:

```powershell
dotnet run --project .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj
```

Build the canonical solution:

```powershell
dotnet build .\ShellKrypt.slnx
```

Run tests:

```powershell
dotnet test .\ShellKrypt.slnx
```

## 3. Environment Variables

ShellKrypt does not require environment variables for normal use.

| Variable | Required | Scope | Description |
|---|---:|---|---|
| `SHELLKRYPT_APPROOT` | No | local development/tests | Overrides the app-data root used for vault registry, settings, exports, and suggested vault paths |

Rules:

- `.env` files are local only and must not be committed.
- `SHELLKRYPT_APPROOT` must not point at a directory containing real user data during automated tests.
- Secret-handling rules are defined in `SECURITY.md`.

## 4. Common Commands

| Command | Purpose |
|---|---|
| `dotnet restore .\ShellKrypt.slnx` | Restore solution dependencies |
| `dotnet build .\ShellKrypt.slnx` | Build workload-neutral desktop/shared/test projects |
| `dotnet test .\ShellKrypt.slnx` | Run all tests |
| `dotnet run --project .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj` | Run the desktop app |
| `dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android` | Build Android app head |
| `dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -t:Run -f net10.0-android` | Build and deploy Android app head |
| `dotnet list .\ShellKrypt.slnx package --vulnerable --include-transitive` | Dependency vulnerability check |

## 5. Testing

Run all checks:

```powershell
dotnet build .\ShellKrypt.slnx
dotnet test .\ShellKrypt.slnx
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android
```

Run focused checks:

```powershell
dotnet test .\ShellKrypt.Tests\ShellKrypt.Tests.csproj --filter FullyQualifiedName~AesGcmBlob
dotnet test .\ShellKrypt.Tests\ShellKrypt.Tests.csproj --filter FullyQualifiedName~Vault
```

Testing rules:

- Add unit tests for changed domain/application logic.
- Add integration tests for changed persistence, crypto, import/export, or path guard behavior.
- Add manual desktop checks for changed user-visible flows.
- Use only synthetic fixtures and temporary vaults.

## 6. Database And Fixtures

- Local database: `.skvault` files under `%APPDATA%\ShellKrypt\Vaults` by default, or `SHELLKRYPT_APPROOT`.
- Migration command: none; schema is created/updated by infrastructure code.
- Seed command: none.
- Test database: temporary local vault files created by tests.
- Fixture directory: none dedicated today.

Rules:

- Generated local database files are not committed.
- Fixtures must not contain real user data.
- Tests should not depend on production services or production data.

## 7. Branching And Commits

- Branch naming: use `codex/` prefix for coding-agent branches unless the user asks otherwise.
- Commit style: concise imperative summary; use a body when the change benefits from detail.
- Changelog: update `CHANGELOG.md` for meaningful product, security, architecture, or documentation changes.
- Push policy: push only when explicitly requested.

## 8. Debugging

Useful locations:

- App data root: `%APPDATA%\ShellKrypt` unless `SHELLKRYPT_APPROOT` is set.
- Vaults: `%APPDATA%\ShellKrypt\Vaults`
- Exports: `%APPDATA%\ShellKrypt\Exports`
- Settings: `%APPDATA%\ShellKrypt\settings.json`
- Vault registry: `%APPDATA%\ShellKrypt\vaults.json`

Useful inspection commands:

```powershell
git status --short
rg "SearchText" .\ShellKrypt.Application .\ShellKrypt.Desktop
dotnet build .\ShellKrypt.slnx --artifacts-path .\artifacts
```

Logging rules:

- Do not log passwords, API keys, OTP seeds, card numbers, CVCs, backup passphrases, master passwords, private notes, or plaintext export contents.
- Prefer file basenames instead of full paths in activity logs.

## 9. Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| Desktop build cannot copy `ShellKrypt.Desktop.exe` or `.dll` | App is running and locking `bin\Debug` output | Close the app or build with alternate `OutDir` |
| Android build fails with workload error | Android workload missing | Run `dotnet workload restore .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj` |
| Android device does not launch app | Device install blocked or no target selected | Confirm phone install prompt, enable USB debugging/install via USB, or select emulator |
| iOS build fails on Windows | iOS requires supported Apple tooling | Build on macOS with Xcode and .NET iOS workload |
| Vault does not unlock | Wrong password or corrupted vault metadata | Verify password, try backup, do not claim recovery is possible |
| Plaintext export appears in repo status | Export path points inside repo | Move/delete export and keep generated outputs uncommitted |

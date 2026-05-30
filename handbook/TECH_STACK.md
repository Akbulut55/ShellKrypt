# ShellKrypt: Tech Stack

This document records the approved technical stack and the reasons behind major tooling choices. Use it to avoid re-deciding the same framework, runtime, database, deployment, and testing questions in every feature.

## 1. Stack Summary

| Layer | Choice | Status | Notes |
|---|---|---|---|
| Language | C# | Locked | Nullable reference types enabled in active projects |
| Runtime | .NET 10 | Locked | Desktop, shared libraries, tests, and mobile heads target .NET 10 variants |
| UI framework | Avalonia 12 | Locked | Desktop and mobile UI use Avalonia |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | Approved | Desktop viewmodels use toolkit-generated properties and commands |
| Database | SQLite | Locked | Local `.skvault` files use SQLite through `Microsoft.Data.Sqlite` |
| Crypto | AES-GCM and Argon2id | Locked | AES-GCM payload encryption, Argon2id key derivation |
| QR/image | ZXing.Net and ImageSharp | Approved | Authenticator QR screenshot/paste import |
| Testing | xUnit | Approved | `ShellKrypt.Tests` covers core, infrastructure, application, and selected UI/shared behavior |
| Mobile | Avalonia Android/iOS heads | Active foundation | Shared mobile UI with Android/iOS app heads |
| Deployment | Local desktop/mobile packages | Planned | Windows publish works; signing/installer/store work remains |

## 2. Runtime And Tooling

- .NET SDK: .NET 10
- Package manager: NuGet through `dotnet restore`
- Language version: SDK default for .NET 10
- Formatting: project style plus compiler validation; no dedicated formatter is currently configured
- Linting: compiler warnings and tests; no dedicated analyzer gate is currently configured
- Solution: `ShellKrypt.slnx` is the canonical root solution

Rules:

- Keep the default `dotnet build .\ShellKrypt.slnx` workload-neutral on Windows.
- Build Android and iOS app heads directly by project file.
- Do not commit generated `bin/`, `obj/`, `publish/`, `artifacts*/`, local vaults, backups, or exports.

## 3. Desktop Frontend

- Framework: Avalonia 12
- Pattern: MVVM
- Controls: XAML views with viewmodels in `ShellKrypt.Desktop`
- Shared visual primitives: `ShellKrypt.UI.Shared`
- Theme: Stitch-inspired dark theme with cyan accent, compact cards/tables, rounded controls, and standardized palette keys
- Icons: primarily text initials and built-in UI symbols today; use a consistent icon system if broader iconography is introduced

Frontend constraints:

- Keep desktop views consistent with shared theme resources.
- Keep table views visually consistent across Web Logins, Credit Cards, API Keys, Activity Logs, and All Items.
- Use shared modal primitives such as `ModalShell` where item editors share structure.
- Avoid platform behavior in shared domain/application logic.

## 4. Mobile Frontend

- Shared shell: `ShellKrypt.Mobile` targeting `net10.0`
- Android head: `ShellKrypt.Mobile.Android` targeting `net10.0-android`
- iOS head: `ShellKrypt.Mobile.iOS` targeting `net10.0-ios`
- UX direction: mobile card lists, bottom navigation or drawer, full-screen detail/edit pages, guided import/export flows

Mobile constraints:

- Keep one shared mobile UX for Android and iOS.
- Put platform-specific clipboard, secure storage, file picker, share sheet, image picker, lifecycle, privacy screen, and biometric behavior behind adapters.
- Do not port desktop tables or modal-heavy flows directly to small screens.

## 5. Backend And Services

ShellKrypt has no remote backend today.

- API style: none
- Server framework: none
- Background jobs: none
- File handling: local files through desktop/mobile platform services
- Provider integrations: none by default

Service constraints:

- Do not add network sync, account auth, or remote recovery without changing `handbook/IDEA.md`, `SECURITY.md`, and `handbook/PLAN.md`.
- Keep local-only behavior as the default product model.

## 6. Data And Persistence

- Primary database: SQLite `.skvault` files
- Query layer: `Microsoft.Data.Sqlite`
- Migrations: code-managed schema creation and targeted compatibility handling
- App metadata: JSON files under the ShellKrypt app-data directory
- Backups: encrypted `.skbx` package files
- Plaintext exports: explicit decrypted JSON reports
- Cache: none

Rules:

- Sensitive item payloads are encrypted before storage.
- Activity logs are encrypted and stored per vault.
- App metadata stores must not contain raw secrets.
- Persistence rules are detailed in `handbook/DATABASE.md`.

## 7. Crypto And Security Libraries

- AES-GCM: `System.Security.Cryptography.AesGcm`
- Argon2id: `Konscious.Security.Cryptography.Argon2`
- Encrypted blob helper: `ShellKrypt.Infrastructure.Crypto.AesGcmBlob`
- QR scanning: `ZXing.Net`
- Image handling: `SixLabors.ImageSharp`

Rules:

- Do not introduce new crypto primitives casually.
- Prefer central helpers and existing services over per-feature crypto code.
- Security changes need tests for tampering, wrong keys, corrupted metadata, import limits, and plaintext leakage.

## 8. Testing

| Test Type | Tool | Required For |
|---|---|---|
| Unit | xUnit | Core/application logic, filters, summaries, settings normalization |
| Integration | xUnit with temporary files/SQLite | Vault storage, import/export, activity logs, crypto flows |
| UI adapter | xUnit where practical, manual desktop smoke test otherwise | Viewmodel and shared UI logic |
| Mobile build | `dotnet build` Android project | Mobile head compile validation |
| Security checks | xUnit plus manual review | Crypto, import limits, logging, plaintext export, deletion guards |

## 9. Deployment

- Development: local `dotnet run`
- Preview: local build/publish artifacts
- Production: not established
- Secrets manager: none currently, because there is no backend
- Windows build command: `dotnet publish .\ShellKrypt.Desktop\ShellKrypt.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64`
- Android build command: `dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -f net10.0-android`
- iOS build command: `dotnet build .\ShellKrypt.Mobile.iOS\ShellKrypt.Mobile.iOS.csproj -f net10.0-ios`

Deployment constraints:

- Windows code signing is not configured yet.
- Installer/update delivery is not configured yet.
- Android/iOS signing and store packaging are not ready.
- Encryption export compliance and terms/privacy/disclaimer text should be handled before commercial distribution.

## 10. Stack Decisions

| Decision | Why | Alternatives Rejected | Date |
|---|---|---|---|
| Use .NET and Avalonia | One C# codebase can support desktop and shared mobile UI | Native WPF-only desktop, web app first | 2026-05-30 |
| Store vaults as local SQLite files | A single portable database file supports local-only encrypted storage | Cloud database, loose JSON files | 2026-05-30 |
| Keep one shared mobile shell | Avoid duplicating Android/iOS UX and business logic | Separate Android and iOS shells | 2026-05-30 |
| Keep iOS out of the root solution build | Windows default builds should not require optional iOS workloads | A second root solution, one all-platform solution | 2026-05-30 |

Material decisions should also be added to `handbook/DECISIONS.md`.

## 11. Upgrade Policy

- Dependency update rhythm: per release or when security fixes require it
- Security update policy: apply promptly after validation
- Breaking change policy: avoid vault format breaks unless accompanied by explicit migration and backup guidance
- Minimum supported runtime: .NET 10 for the current codebase
- Deprecated tools to avoid: ad hoc crypto, global plaintext activity logs, unrelated root solution files, and client-side provider secrets

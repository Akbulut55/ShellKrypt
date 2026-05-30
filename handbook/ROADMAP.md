# ShellKrypt: Roadmap

This roadmap describes product and delivery sequencing. It should stay higher level than `handbook/PLAN.md`: use this file for milestones and intent, and `handbook/PLAN.md` for implementation details and acceptance criteria.

## 1. Current Focus

Current milestone:

> Desktop stabilization and release readiness.

Goal:

> Make the Windows desktop app consistent, reliable, and honest enough for private pre-release use while keeping the mobile foundation ready for focused expansion.

Target users:

- Local desktop users managing sensitive records without cloud sync.
- The project owner and private testers validating workflows before public release.

## 2. Milestones

| Milestone | Goal | Status | Target |
|---|---|---|---|
| M0 - Vault Foundation | Create, unlock, encrypt, store, and reopen local vaults | Done | Completed |
| M1 - Core Item Workspaces | Web logins, cards, API keys, authenticators, notes, generator, audit, settings, activity logs | Done | Completed |
| M2 - Security And UX Hardening | Activity logs in vault DB, export warnings, path guards, UI standardization, modal consistency | Active | Pre-release |
| M3 - Mobile MVP | Real Android/iOS shared mobile flows for core vault and web login workflows | Planned | After desktop stabilization |
| M4 - Commercial Release Prep | Signing, installer, terms/privacy/disclaimer docs, support, smoke tests, export-compliance review | Planned | Before public 1.0 |
| M5 - Public 1.0 | Stable supported public release | Planned | After M4 |

## 3. Now

Work that should happen next:

- Finish desktop UI consistency across item screens, settings, and responsive layouts.
- Split remaining large desktop viewmodels where shared logic should live in Application.
- Keep README, handbook docs, security docs, and changelog current.
- Validate build/test/mobile Android build regularly.
- Prepare a release smoke-test checklist that matches the current product.

## 4. Next

Work that matters after the current milestone:

- Build real mobile Web Login list/detail/add/edit.
- Add mobile settings security pages.
- Add mobile Notes, Cards, API Keys, Authenticator, backup/restore/export.
- Add localization infrastructure and initial non-English language support.
- Validate macOS/Linux desktop behavior if those platforms are considered for release.

## 5. Later

Ideas that are intentionally not part of the current plan:

- Cloud sync or optional sync provider.
- Browser extension/autofill.
- Team/shared vaults.
- Biometric unlock as a convenience feature.
- Importers for more password manager formats.
- Hardware security key support.

## 6. Explicit Non-Roadmap Items

These are not planned unless `handbook/IDEA.md` or `handbook/PLAN.md` changes:

- Remote account recovery.
- Server-hosted vaults.
- Mandatory cloud sync.
- Enterprise administration.
- Claims of external audit, certification, or zero-risk security.

## 7. Release Criteria

### Private Pre-Release

- Build and tests pass.
- All current desktop item workflows are usable.
- Backup/restore, plaintext export warning, clipboard clearing, and vault deletion are smoke-tested.
- Known limitations are documented.

### Beta

- Installer/signing story is decided for Windows.
- Release smoke test is repeatable.
- Security copy and no-recovery warning are finalized.
- Activity logs and exports are checked for secret leakage.
- At least one mobile or cross-platform direction is validated.

### Production 1.0

- Public terms/privacy/disclaimer docs and support channel exist.
- Code signing and update delivery are ready.
- Dependency vulnerability checks are part of release.
- Localization decision is made.
- Commercial encryption export compliance is reviewed.
- Critical workflows have stable automated or manual regression coverage.

## 8. Dependencies

| Dependency | Needed For | Owner | Status |
|---|---|---|---|
| .NET 10 SDK | Build/test/run | Project owner | Active |
| Avalonia 12 | Desktop/mobile UI | Project owner | Active |
| Android workload and SDK | Android testing | Project owner | Active for local builds |
| macOS/Xcode/iOS workload | iOS testing | Project owner | Needed later |
| Code-signing certificate | Commercial Windows release | Project owner | Not started |
| Terms/privacy/support docs | Commercial release | Project owner | Draft terms, privacy, disclaimer, license, and notice prepared |
| External security review | Stronger security claims | Project owner | Not started |

## 9. Roadmap Risks

| Risk | Affected Milestone | Mitigation |
|---|---|---|
| Mobile scope grows before desktop stabilizes | M3 | Keep mobile MVP narrow and reuse Application logic |
| Release packaging takes longer than feature work | M4 | Track signing, installer, update, terms/privacy/disclaimer docs, and support as release blockers |
| Security copy overpromises | M2/M4 | Keep claims limited and update `SECURITY.md` |
| Missing localization delays broad release | M4/M5 | Add language infrastructure before public 1.0 |
| iOS build tooling is unavailable on Windows | M3 | Build iOS on supported Apple environment and keep root solution workload-neutral |

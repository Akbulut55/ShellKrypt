# ShellKrypt.Mobile

Shared Android/iOS mobile shell.

This project contains the shared mobile UI, viewmodels, and platform contracts. The real app heads live in:

- `ShellKrypt.Mobile.Android`
- `ShellKrypt.Mobile.iOS`

The shared shell intentionally targets `net10.0` so it remains testable without mobile workloads. The Android and iOS heads target `net10.0-android` and `net10.0-ios`.

Architecture rules:

- Keep one shared mobile UX for Android and iOS.
- Keep vault logic in `ShellKrypt.Core` and `ShellKrypt.Infrastructure`.
- Put platform-specific clipboard, secure storage, file picker, image picker, privacy screen, share sheet, and biometric behavior behind `Platform`.
- Use mobile list cards and full-screen pages. Do not port desktop tables or modal-heavy flows directly.
- Keep cloud sync disabled by default. Mobile vault import/export is user-initiated.

Run targets:

- Android: `dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -t:Run -f net10.0-android`
- iOS: build/run from macOS with Xcode and the .NET iOS workload, using `ShellKrypt.Mobile.iOS`.

Docs:

- `Docs/MobileArchitecture.md`
- `Docs/MobileTargets.md`
- `Docs/VaultStorage.md`
- `Docs/MobileTesting.md`

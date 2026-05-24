# Mobile Targets

Shared shell target:

- `net10.0`

Real app head targets:

- `ShellKrypt.Mobile.Android`: `net10.0-android`
- `ShellKrypt.Mobile.iOS`: `net10.0-ios`

This keeps regular desktop solution builds working while still providing real Android/iOS app projects.

Already present:

- Android manifest and permissions
- Android `MainActivity`
- Android package metadata
- iOS `Info.plist`
- iOS `AppDelegate`
- iOS package metadata

Still needed before store release:

- final app icons and splash assets
- Android signing key and Play Store configuration
- iOS signing, provisioning, and App Store configuration
- device/emulator build scripts
- real Android/iOS platform service implementations

Do not split the UI shell per platform unless a platform requirement makes the shared shell impractical.

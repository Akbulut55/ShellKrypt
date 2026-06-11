# ShellKrypt.Mobile.Android

Real Android app head for the shared ShellKrypt mobile shell.

Requirements:

- .NET Android workload
- Android SDK
- Android emulator or device

Run:

```powershell
dotnet build .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -t:Run -f net10.0-android
```

On Xiaomi/MIUI devices, enable developer options for ADB installs before running:

- USB debugging
- Install via USB
- USB debugging (Security settings), if present

If the run target fails with `INSTALL_FAILED_USER_RESTRICTED: Install canceled by user`, the phone blocked the install. Confirm the install prompt on the phone or enable the settings above.

Publish package:

```powershell
dotnet publish .\ShellKrypt.Mobile.Android\ShellKrypt.Mobile.Android.csproj -c Release -f net10.0-android
```

Debug builds use APK packaging for device deployment. Release builds use AAB packaging for store distribution.

Before store release, add final icons, signing configuration, and release keystore handling.

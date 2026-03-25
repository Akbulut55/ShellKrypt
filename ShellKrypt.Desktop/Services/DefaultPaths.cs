using System;

namespace ShellKrypt.Desktop.Services;

public static class DefaultPaths
{
    public static string DefaultVaultPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShellKrypt",
            "Vaults",
            "ShellKrypt.skvault"
        );

    public static string SettingsPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShellKrypt",
            "settings.json"
        );
}

namespace ShellKrypt.Mobile.Storage;

public sealed record MobileVaultStoragePolicy(
    MobileVaultStorageMode Mode,
    bool AllowsUserImport,
    bool AllowsUserExport,
    bool AllowsManualShare,
    bool EnablesCloudSync)
{
    public static MobileVaultStoragePolicy Default { get; } = new(
        MobileVaultStorageMode.AppPrivateLocalVaults,
        AllowsUserImport: true,
        AllowsUserExport: true,
        AllowsManualShare: true,
        EnablesCloudSync: false);

    public string Summary =>
        "Mobile vaults are local app-private .skvault files by default. Import, export, and manual sharing are user-initiated. Cloud sync is not enabled by default.";
}

public enum MobileVaultStorageMode
{
    AppPrivateLocalVaults
}

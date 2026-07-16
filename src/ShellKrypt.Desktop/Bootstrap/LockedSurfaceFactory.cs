using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Bootstrap;

internal sealed class LockedSurfaceFactory(
    IVaultService vaultService,
    VaultRegistryService vaultRegistryService,
    IVaultSessionController session,
    IDesktopDialogService dialogs,
    ISecureClipboardService clipboard,
    IActivityRecorder activity,
    IDesktopSettingsController settings,
    LocalizationService localization,
    IDesktopFileService files)
{
    public WelcomeViewModel CreateWelcome(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, dialogs, clipboard, activity, settings, localization, files);

    public CreateVaultViewModel CreateCreateVault(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, dialogs, activity, localization, files);

    public UnlockViewModel CreateUnlock(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, localization);
}

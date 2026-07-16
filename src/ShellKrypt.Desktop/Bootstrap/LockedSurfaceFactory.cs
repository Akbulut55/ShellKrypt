using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Services.Runtime;
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
    LocalizationService localization)
{
    public WelcomeViewModel CreateWelcome(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, dialogs, clipboard, activity, settings, localization);

    public CreateVaultViewModel CreateCreateVault(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, dialogs, activity, localization);

    public UnlockViewModel CreateUnlock(IDesktopNavigation navigation) => new(vaultService, vaultRegistryService, session, navigation, localization);
}

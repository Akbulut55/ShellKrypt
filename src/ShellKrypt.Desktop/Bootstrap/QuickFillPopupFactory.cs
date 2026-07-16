using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Features.QuickFill;
using ShellKrypt.Desktop.Features.QuickFill;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Bootstrap;

internal sealed class QuickFillPopupFactory(DesktopServiceCatalog services)
{
    public void Open(IDesktopNavigation navigation, QuickFillTargetContext target, IDisposable focusSuppression)
    {
        var popup = new QuickFillPopupWindow();
        var viewModel = new QuickFillPopupViewModel(
            services.DesktopFeatures,
            navigation,
            services.SessionSecurity,
            services.VaultRegistryService,
            services.VaultService,
            services.QuickFillEntryService,
            services.WebLoginService,
            services.CardService,
            services.ApiKeyService,
            services.AuthenticatorEntryService,
            services.OneTimePasswordGenerator,
            services.AutoTypeService,
            target);

        viewModel.CloseRequested += (_, _) => popup.Close();
        popup.Closed += (_, _) => focusSuppression.Dispose();
        popup.DataContext = viewModel;
        popup.Show();
        _ = viewModel.LoadAsync();
    }
}

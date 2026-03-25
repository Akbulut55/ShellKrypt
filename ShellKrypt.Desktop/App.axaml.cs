using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Views;

namespace ShellKrypt.Desktop
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                var mainWindowViewModel = new MainWindowViewModel();
                ApplyTheme(mainWindowViewModel.ThemeMode);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel,
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void ApplyTheme(AppThemeMode mode)
        {
            RequestedThemeVariant = mode == AppThemeMode.Light ? ThemeVariant.Light : ThemeVariant.Dark;

            if (mode == AppThemeMode.Light)
            {
                UpdateBrush("AppBackgroundBrush", "#f3f7fb");
                UpdateBrush("AppBackgroundSoftBrush", "#e8eff7");
                UpdateBrush("SurfaceBrush", "#ffffff");
                UpdateBrush("SurfaceRaisedBrush", "#f7fbff");
                UpdateBrush("SurfaceElevatedBrush", "#edf3fa");
                UpdateBrush("SidebarBrush", "#eef4fa");
                UpdateBrush("BorderBrushSoft", "#d7e1ec");
                UpdateBrush("BorderBrushStrong", "#bfd0df");
                UpdateBrush("TextPrimaryBrush", "#172230");
                UpdateBrush("TextMutedBrush", "#66778b");
                UpdateBrush("AccentBrush", "#2f8397");
                UpdateBrush("AccentMutedBrush", "#dceef3");
                UpdateBrush("SuccessBrush", "#2f8f6c");
                UpdateBrush("WarningBrush", "#b2803a");
                UpdateBrush("DangerBrush", "#c65d62");
                return;
            }

            UpdateBrush("AppBackgroundBrush", "#0e151d");
            UpdateBrush("AppBackgroundSoftBrush", "#111a23");
            UpdateBrush("SurfaceBrush", "#17212b");
            UpdateBrush("SurfaceRaisedBrush", "#1b2732");
            UpdateBrush("SurfaceElevatedBrush", "#22303d");
            UpdateBrush("SidebarBrush", "#101923");
            UpdateBrush("BorderBrushSoft", "#223342");
            UpdateBrush("BorderBrushStrong", "#31495d");
            UpdateBrush("TextPrimaryBrush", "#eaf2fb");
            UpdateBrush("TextMutedBrush", "#9cadbf");
            UpdateBrush("AccentBrush", "#3c94a7");
            UpdateBrush("AccentMutedBrush", "#163744");
            UpdateBrush("SuccessBrush", "#45a07a");
            UpdateBrush("WarningBrush", "#c3924f");
            UpdateBrush("DangerBrush", "#d26b6f");
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private void UpdateBrush(string key, string color)
        {
            if (TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
                brush.Color = Color.Parse(color);
        }
    }
}

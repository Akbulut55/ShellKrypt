using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
                UpdateBrush("AppBackgroundBrush", "#f3f4f4");
                UpdateBrush("AppBackgroundSoftBrush", "#e8ebeb");
                UpdateBrush("SurfaceBrush", "#ffffff");
                UpdateBrush("SurfaceRaisedBrush", "#f6f7f7");
                UpdateBrush("SurfaceElevatedBrush", "#edf0ef");
                UpdateBrush("SidebarBrush", "#eeefee");
                UpdateBrush("BorderBrushSoft", "#33a5b2ad");
                UpdateBrush("BorderBrushStrong", "#6682958e");
                UpdateBrush("WindowOutlineBrush", "#d9e0de");
                UpdateBrush("TextPrimaryBrush", "#1f2624");
                UpdateBrush("TextMutedBrush", "#62716c");
                UpdateBrush("AccentBrush", "#19cdb6");
                UpdateBrush("AccentMutedBrush", "#d8f0ed");
                UpdateBrush("AccentForegroundBrush", "#073932");
                UpdateBrush("SuccessBrush", "#1a7f67");
                UpdateBrush("WarningBrush", "#b56e29");
                UpdateBrush("DangerBrush", "#c35a61");
                return;
            }

            UpdateBrush("AppBackgroundBrush", "#131313");
            UpdateBrush("AppBackgroundSoftBrush", "#0e0e0e");
            UpdateBrush("SurfaceBrush", "#201f1f");
            UpdateBrush("SurfaceRaisedBrush", "#2a2a2a");
            UpdateBrush("SurfaceElevatedBrush", "#353534");
            UpdateBrush("SidebarBrush", "#1c1b1b");
            UpdateBrush("BorderBrushSoft", "#333c4a46");
            UpdateBrush("BorderBrushStrong", "#66859490");
            UpdateBrush("WindowOutlineBrush", "#272626");
            UpdateBrush("TextPrimaryBrush", "#e5e2e1");
            UpdateBrush("TextMutedBrush", "#bacac5");
            UpdateBrush("AccentBrush", "#57f1db");
            UpdateBrush("AccentMutedBrush", "#1a4f47");
            UpdateBrush("AccentForegroundBrush", "#003731");
            UpdateBrush("SuccessBrush", "#9cd1c6");
            UpdateBrush("WarningBrush", "#ffd1aa");
            UpdateBrush("DangerBrush", "#ffb4ab");
        }

        private void UpdateBrush(string key, string color)
        {
            if (TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
                brush.Color = Color.Parse(color);
        }
    }
}

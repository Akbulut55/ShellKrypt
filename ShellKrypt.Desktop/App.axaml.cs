using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ShellKrypt.Application.Settings;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Views;
using ShellKrypt.UI.Shared.Theming;

namespace ShellKrypt.Desktop
{
    public partial class App : Avalonia.Application
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
            var brushes = mode == AppThemeMode.Light
                ? ShellKryptThemePalettes.Light
                : ShellKryptThemePalettes.Dark;

            foreach (var brush in brushes)
                UpdateBrush(brush.Key, brush.Value);

            UpdateAccentGradient(
                brushes["AccentBrush"],
                brushes["AccentPressedBrush"]);
        }

        private void UpdateBrush(string key, string color)
        {
            if (TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
                brush.Color = Color.Parse(color);
        }

        private void UpdateAccentGradient(string startColor, string endColor)
        {
            if (!TryGetResource("AccentGradientBrush", null, out var resource) ||
                resource is not LinearGradientBrush gradient ||
                gradient.GradientStops.Count < 2)
            {
                return;
            }

            gradient.GradientStops[0].Color = Color.Parse(startColor);
            gradient.GradientStops[1].Color = Color.Parse(endColor);
        }
    }
}

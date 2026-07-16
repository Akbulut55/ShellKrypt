using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Bootstrap;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Resources.Theming;

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
                var mainWindowViewModel = DesktopBootstrap.CreateMainWindowViewModel();
                ApplyTheme(mainWindowViewModel.ThemeId);
                ApplyLocalization(mainWindowViewModel.Localization);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel,
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void ApplyTheme(string? themeId)
        {
            var theme = ShellKryptThemePalettes.GetById(themeId);
            RequestedThemeVariant = theme.BaseVariant;

            foreach (var brush in theme.Palette)
                UpdateBrush(brush.Key, brush.Value);

            UpdateAccentGradient(
                theme.Palette["AccentBrush"],
                theme.Palette["AccentPressedBrush"]);

            UpdateGradient(
                "CardPreviewGradientBrush",
                theme.Palette["CardPreviewGradientStartColor"],
                theme.Palette["CardPreviewGradientEndColor"]);
        }

        public void ApplyLocalization(LocalizationService localization)
        {
            foreach (var pair in localization.GetCurrentStrings())
                Resources[$"Loc.{pair.Key}"] = pair.Value;
        }

        private void UpdateBrush(string key, string color)
        {
            if (TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
                brush.Color = Color.Parse(color);
            else if (resource is Color)
                Resources[key] = Color.Parse(color);
        }

        private void UpdateAccentGradient(string startColor, string endColor)
        {
            UpdateGradient("AccentGradientBrush", startColor, endColor);
        }

        private void UpdateGradient(string resourceKey, string startColor, string endColor)
        {
            if (!TryGetResource(resourceKey, null, out var resource) ||
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

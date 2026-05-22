using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System.Collections.Generic;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Views;

namespace ShellKrypt.Desktop
{
    public partial class App : Application
    {
        private static readonly IReadOnlyDictionary<string, string> DarkBrushes = new Dictionary<string, string>
        {
            ["AppBackgroundBrush"] = "#131313",
            ["AppBackgroundSoftBrush"] = "#0e0e0e",
            ["SurfaceBrush"] = "#201f1f",
            ["SurfaceRaisedBrush"] = "#2a2a2a",
            ["SurfaceElevatedBrush"] = "#353534",
            ["SurfaceHoverBrush"] = "#353534",
            ["SidebarBrush"] = "#1c1b1b",
            ["BorderBrushSoft"] = "#333c4a46",
            ["BorderBrushStrong"] = "#66859490",
            ["WindowOutlineBrush"] = "#272626",
            ["TextPrimaryBrush"] = "#e5e2e1",
            ["TextMutedBrush"] = "#bacac5",
            ["TextInverseBrush"] = "#050505",
            ["AccentBrush"] = "#57f1db",
            ["AccentHoverBrush"] = "#63f5e1",
            ["AccentPressedBrush"] = "#2dd4bf",
            ["AccentMutedBrush"] = "#1a4f47",
            ["AccentForegroundBrush"] = "#003731",
            ["SuccessBrush"] = "#9cd1c6",
            ["SuccessMutedBrush"] = "#174544",
            ["SuccessForegroundBrush"] = "#57f1db",
            ["WarningBrush"] = "#ffd1aa",
            ["WarningMutedBrush"] = "#3a3228",
            ["WarningForegroundBrush"] = "#ffd1aa",
            ["DangerBrush"] = "#ffb4ab",
            ["DangerMutedBrush"] = "#3a2426",
            ["DangerPanelBrush"] = "#261e1112",
            ["DangerBorderBrush"] = "#55ffb4ab",
            ["DangerForegroundBrush"] = "#050505",
            ["DangerHoverBrush"] = "#ff8f8f",
            ["InfoBrush"] = "#bacac5",
            ["InfoMutedBrush"] = "#2a2a2a",
            ["TableShellBrush"] = "#0e0e0e",
            ["TableHeaderBrush"] = "#201f1f",
            ["TableRowBrush"] = "#2a2a2a",
            ["TableRowHoverBrush"] = "#353534",
            ["TableRowSelectedBrush"] = "#353534",
            ["TableBorderBrush"] = "#171f1d",
            ["TableFooterBrush"] = "#201f1f",
            ["InputBackgroundBrush"] = "#0e0e0e",
            ["InputBorderBrush"] = "#333c4a46",
            ["InputHoverBorderBrush"] = "#333c4a46",
            ["InputFocusBorderBrush"] = "#57f1db",
            ["ModalCardBrush"] = "#201f1f",
            ["ModalBorderBrush"] = "#663c4a46",
            ["OverlayScrimBrush"] = "#b0000000",
            ["OverlaySoftScrimBrush"] = "#70000000",
            ["ResizeHitTestBrush"] = "#01000000",
            ["WatermarkBrush"] = "#0fffffff",
            ["QuoteBackgroundBrush"] = "#101918",
            ["HeroGlowPrimaryBrush"] = "#1800ffff",
            ["HeroGlowSecondaryBrush"] = "#101a4f47",
            ["CaptionButtonForegroundBrush"] = "#cccccc",
            ["CaptionButtonHoverBrush"] = "#2a2d2e",
            ["CaptionButtonHoverForegroundBrush"] = "#ffffff",
            ["CaptionButtonCloseHoverBrush"] = "#c42b1c",
            ["TypeWebBackgroundBrush"] = "#223b36",
            ["TypeWebForegroundBrush"] = "#9cd1c6",
            ["TypeCardBackgroundBrush"] = "#4a3827",
            ["TypeCardForegroundBrush"] = "#ffd1aa",
            ["TypeNoteBackgroundBrush"] = "#174544",
            ["TypeNoteForegroundBrush"] = "#57f1db",
            ["TypeAuthenticatorBackgroundBrush"] = "#1c3e4a",
            ["TypeAuthenticatorForegroundBrush"] = "#9fe8ff",
            ["TypeApiKeyBackgroundBrush"] = "#174544",
            ["TypeApiKeyForegroundBrush"] = "#57f1db",
            ["StrengthNoneBrush"] = "#7b8a87",
            ["StrengthWeakBrush"] = "#ff7a7a",
            ["StrengthFairBrush"] = "#ffb35a",
            ["StrengthStrongBrush"] = "#74f0dd",
            ["StrengthSecureBrush"] = "#4ff0df"
        };

        private static readonly IReadOnlyDictionary<string, string> LightBrushes = new Dictionary<string, string>
        {
            ["AppBackgroundBrush"] = "#f3f4f4",
            ["AppBackgroundSoftBrush"] = "#e8ebeb",
            ["SurfaceBrush"] = "#ffffff",
            ["SurfaceRaisedBrush"] = "#f6f7f7",
            ["SurfaceElevatedBrush"] = "#edf0ef",
            ["SurfaceHoverBrush"] = "#edf0ef",
            ["SidebarBrush"] = "#eeefee",
            ["BorderBrushSoft"] = "#33a5b2ad",
            ["BorderBrushStrong"] = "#6682958e",
            ["WindowOutlineBrush"] = "#d9e0de",
            ["TextPrimaryBrush"] = "#1f2624",
            ["TextMutedBrush"] = "#62716c",
            ["TextInverseBrush"] = "#ffffff",
            ["AccentBrush"] = "#19cdb6",
            ["AccentHoverBrush"] = "#20dcc4",
            ["AccentPressedBrush"] = "#0faf9c",
            ["AccentMutedBrush"] = "#d8f0ed",
            ["AccentForegroundBrush"] = "#073932",
            ["SuccessBrush"] = "#1a7f67",
            ["SuccessMutedBrush"] = "#d8f0ed",
            ["SuccessForegroundBrush"] = "#1a7f67",
            ["WarningBrush"] = "#b56e29",
            ["WarningMutedBrush"] = "#fff2df",
            ["WarningForegroundBrush"] = "#8a4f16",
            ["DangerBrush"] = "#c35a61",
            ["DangerMutedBrush"] = "#ffe6e8",
            ["DangerPanelBrush"] = "#fff0f1",
            ["DangerBorderBrush"] = "#66c35a61",
            ["DangerForegroundBrush"] = "#ffffff",
            ["DangerHoverBrush"] = "#a9464d",
            ["InfoBrush"] = "#62716c",
            ["InfoMutedBrush"] = "#edf0ef",
            ["TableShellBrush"] = "#e8ebeb",
            ["TableHeaderBrush"] = "#ffffff",
            ["TableRowBrush"] = "#f6f7f7",
            ["TableRowHoverBrush"] = "#edf0ef",
            ["TableRowSelectedBrush"] = "#edf0ef",
            ["TableBorderBrush"] = "#d9e0de",
            ["TableFooterBrush"] = "#ffffff",
            ["InputBackgroundBrush"] = "#ffffff",
            ["InputBorderBrush"] = "#33a5b2ad",
            ["InputHoverBorderBrush"] = "#6682958e",
            ["InputFocusBorderBrush"] = "#19cdb6",
            ["ModalCardBrush"] = "#ffffff",
            ["ModalBorderBrush"] = "#66a5b2ad",
            ["OverlayScrimBrush"] = "#99000000",
            ["OverlaySoftScrimBrush"] = "#70000000",
            ["ResizeHitTestBrush"] = "#01000000",
            ["WatermarkBrush"] = "#10000000",
            ["QuoteBackgroundBrush"] = "#eef8f6",
            ["HeroGlowPrimaryBrush"] = "#1800b4a7",
            ["HeroGlowSecondaryBrush"] = "#18d8f0ed",
            ["CaptionButtonForegroundBrush"] = "#62716c",
            ["CaptionButtonHoverBrush"] = "#d9e0de",
            ["CaptionButtonHoverForegroundBrush"] = "#1f2624",
            ["CaptionButtonCloseHoverBrush"] = "#c42b1c",
            ["TypeWebBackgroundBrush"] = "#d8f0ed",
            ["TypeWebForegroundBrush"] = "#1a7f67",
            ["TypeCardBackgroundBrush"] = "#fff2df",
            ["TypeCardForegroundBrush"] = "#8a4f16",
            ["TypeNoteBackgroundBrush"] = "#d8f0ed",
            ["TypeNoteForegroundBrush"] = "#168774",
            ["TypeAuthenticatorBackgroundBrush"] = "#e2f4fb",
            ["TypeAuthenticatorForegroundBrush"] = "#22657a",
            ["TypeApiKeyBackgroundBrush"] = "#d8f0ed",
            ["TypeApiKeyForegroundBrush"] = "#168774",
            ["StrengthNoneBrush"] = "#62716c",
            ["StrengthWeakBrush"] = "#c35a61",
            ["StrengthFairBrush"] = "#b56e29",
            ["StrengthStrongBrush"] = "#168774",
            ["StrengthSecureBrush"] = "#0f8f7e"
        };

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
            var brushes = mode == AppThemeMode.Light ? LightBrushes : DarkBrushes;

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

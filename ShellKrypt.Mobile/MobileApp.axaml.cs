using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ShellKrypt.Mobile.ViewModels;
using ShellKrypt.Mobile.Views;
using ShellKrypt.UI.Shared.Theming;

namespace ShellKrypt.Mobile;

public partial class MobileApp : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (TrySetActivityMainViewFactory(ApplicationLifetime))
        {
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = CreateMainView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MobileShellView CreateMainView()
        => new()
        {
            DataContext = new MobileShellViewModel()
        };

    private static bool TrySetActivityMainViewFactory(IApplicationLifetime? lifetime)
    {
        var property = lifetime?.GetType().GetProperty("MainViewFactory");
        if (property is null || !property.CanWrite || property.PropertyType != typeof(Func<Control>))
            return false;

        property.SetValue(lifetime, () => CreateMainView());
        return true;
    }

    public void ApplyTheme(ThemeVariant themeVariant)
    {
        RequestedThemeVariant = themeVariant;
        var brushes = themeVariant == ThemeVariant.Light
            ? ShellKryptThemePalettes.Light
            : ShellKryptThemePalettes.Dark;

        foreach (var brush in brushes)
            UpdateBrush(brush.Key, brush.Value);
    }

    private void UpdateBrush(string key, string color)
    {
        if (TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
            brush.Color = Color.Parse(color);
    }
}

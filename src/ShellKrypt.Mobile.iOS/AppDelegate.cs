using Avalonia;
using Avalonia.Fonts.Inter;
using Avalonia.iOS;
using Foundation;
using ShellKrypt.Mobile;

namespace ShellKrypt.Mobile.iOS;

[Register("AppDelegate")]
public sealed class AppDelegate : AvaloniaAppDelegate<MobileApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder)
            .WithInterFont();
}

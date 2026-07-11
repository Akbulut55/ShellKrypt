using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace ShellKrypt.Mobile.Android;

[Activity(
    Label = "ShellKrypt",
    MainLauncher = true,
    Theme = "@style/MainTheme.NoActionBar",
    ConfigurationChanges =
        ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.UiMode |
        ConfigChanges.KeyboardHidden |
        ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

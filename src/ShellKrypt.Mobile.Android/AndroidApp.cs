using Android.App;
using Android.Runtime;
using Avalonia.Android;
using ShellKrypt.Mobile;

namespace ShellKrypt.Mobile.Android;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<MobileApp>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}

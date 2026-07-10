using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShellKrypt.Desktop.Views;

public partial class AuthenticatorView : UserControl
{
    public AuthenticatorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

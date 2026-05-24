using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShellKrypt.Mobile.Views;

public partial class MobileShellView : UserControl
{
    public MobileShellView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

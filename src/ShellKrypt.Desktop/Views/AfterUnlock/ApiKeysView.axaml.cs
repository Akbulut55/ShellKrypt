using Avalonia.Controls;
using Avalonia.Input;

namespace ShellKrypt.Desktop.Views;

public partial class ApiKeysView : UserControl
{
    public ApiKeysView()
    {
        InitializeComponent();
    }

    private void OnFieldTypeComboBoxPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
    }
}

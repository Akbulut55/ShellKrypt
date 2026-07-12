using Avalonia.Controls;
using Avalonia.Input;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;

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

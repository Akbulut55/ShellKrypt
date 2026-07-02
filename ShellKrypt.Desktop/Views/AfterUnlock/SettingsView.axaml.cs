using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ShellKrypt.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        AddHandler(Control.RequestBringIntoViewEvent, SuppressComboBoxBringIntoView, RoutingStrategies.Tunnel);
    }

    private static void SuppressComboBoxBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (IsComboBoxRequest(e.Source) || IsComboBoxRequest(e.TargetObject))
            e.Handled = true;
    }

    private static bool IsComboBoxRequest(object? value)
    {
        return value is ComboBox or ComboBoxItem ||
               value is Visual visual && visual.FindAncestorOfType<ComboBox>() is not null;
    }
}

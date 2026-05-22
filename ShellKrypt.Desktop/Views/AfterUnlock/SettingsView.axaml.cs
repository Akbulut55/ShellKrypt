using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ShellKrypt.Desktop.ViewModels;

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

    private void OnSettingsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            viewModel.ClosePickers();
    }
}

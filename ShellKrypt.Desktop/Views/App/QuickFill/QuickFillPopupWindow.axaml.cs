using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ShellKrypt.Desktop.Views.App.QuickFill;

public partial class QuickFillPopupWindow : Window
{
    public QuickFillPopupWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

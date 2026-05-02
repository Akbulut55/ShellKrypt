using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Opened += OnOpened;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;
        TextInput += OnTextInput;
        Closing += OnClosing;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.AttachClipboard(Clipboard);
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.HandleWindowActivated();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.HandleWindowDeactivated();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        RecordActivity();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        RecordActivity();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        RecordActivity();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        RecordActivity();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        RecordActivity();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        RecordActivity();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Lock();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
            UpdateMaximizeRestoreButton();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2 && CanResize)
        {
            ToggleMaximizeRestore();
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClicked(object? sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnResizeTopPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.North, e);
    private void OnResizeBottomPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.South, e);
    private void OnResizeLeftPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.West, e);
    private void OnResizeRightPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.East, e);
    private void OnResizeTopLeftPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthWest, e);
    private void OnResizeTopRightPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthEast, e);
    private void OnResizeBottomLeftPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthWest, e);
    private void OnResizeBottomRightPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthEast, e);

    private void RecordActivity()
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordActivity();
    }

    private void ToggleMaximizeRestore()
    {
        if (!CanResize)
            return;

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeRestoreButton()
    {
        if (MaximizeRestoreGlyph is null)
            return;

        MaximizeRestoreGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void BeginResize(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (!CanResize || WindowState == WindowState.Maximized)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        BeginResizeDrag(edge, e);
    }
}

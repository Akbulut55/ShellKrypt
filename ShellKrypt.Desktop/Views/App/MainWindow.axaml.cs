using System;
using Avalonia.Controls;
using Avalonia.Input;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void RecordActivity()
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordActivity();
    }
}

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Shell;

public partial class MainWindow : Window
{
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _trayOpenItem;
    private NativeMenuItem? _trayLockItem;
    private NativeMenuItem? _trayExitItem;
    private MainWindowViewModel? _trackedViewModel;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        Opened += OnOpened;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        DataContextChanged += OnDataContextChanged;
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
        {
            vm.AttachClipboard(Clipboard);
            vm.AttachQuickFillHotkey();
        }
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
        if (e.Key == Key.K &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
            DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            vm.OpenQuickFillPopup();
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        RecordActivity();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isExiting &&
            (e.CloseReason is WindowCloseReason.Undefined or WindowCloseReason.WindowClosing) &&
            DataContext is MainWindowViewModel { CloseToTrayEnabled: true })
        {
            e.Cancel = true;
            Hide();
            UpdateTrayVisibility();
            return;
        }

        _isExiting = true;
        _trayIcon?.Dispose();
        if (DataContext is MainWindowViewModel vm)
            vm.Shutdown();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_trackedViewModel is not null)
        {
            _trackedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _trackedViewModel.Localization.LanguageChanged -= OnLocalizationChanged;
        }

        _trackedViewModel = DataContext as MainWindowViewModel;
        if (_trackedViewModel is not null)
        {
            _trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _trackedViewModel.Localization.LanguageChanged += OnLocalizationChanged;
        }

        UpdateTrayText();
        UpdateTrayVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CloseToTrayEnabled) ||
            e.PropertyName == nameof(MainWindowViewModel.IsUnlocked))
        {
            UpdateTrayVisibility();
        }
    }

    private void OnLocalizationChanged(object? sender, EventArgs e) => UpdateTrayText();

    private void InitializeTrayIcon()
    {
        _trayOpenItem = new NativeMenuItem();
        _trayLockItem = new NativeMenuItem();
        _trayExitItem = new NativeMenuItem();

        _trayOpenItem.Click += (_, _) => ShowFromTray();
        _trayLockItem.Click += (_, _) => LockFromTray();
        _trayExitItem.Click += (_, _) => ExitFromTray();

        var menu = new NativeMenu
        {
            Items =
            {
                _trayOpenItem,
                _trayLockItem,
                _trayExitItem
            }
        };

        _trayIcon = new TrayIcon
        {
            Icon = Icon ?? new WindowIcon(new Bitmap("Assets/main-logo.ico")),
            Menu = menu,
            IsVisible = false
        };
        _trayIcon.Clicked += (_, _) => ShowFromTray();
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        var localization = _trackedViewModel?.Localization;
        if (_trayIcon is not null)
            _trayIcon.ToolTipText = localization?.Get("Tray.Tooltip") ?? "ShellKrypt";
        if (_trayOpenItem is not null)
            _trayOpenItem.Header = localization?.Get("Tray.Open") ?? "Open ShellKrypt";
        if (_trayLockItem is not null)
            _trayLockItem.Header = localization?.Get("Tray.LockVault") ?? "Lock Vault";
        if (_trayExitItem is not null)
            _trayExitItem.Header = localization?.Get("Tray.Exit") ?? "Exit ShellKrypt";
    }

    private void UpdateTrayVisibility()
    {
        if (_trayIcon is null)
            return;

        _trayIcon.IsVisible = _trackedViewModel?.CloseToTrayEnabled == true;
        if (_trayLockItem is not null)
            _trayLockItem.IsEnabled = _trackedViewModel?.IsUnlocked == true;
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    private void LockFromTray()
    {
        if (_trackedViewModel?.IsUnlocked == true)
            _trackedViewModel.Lock();
    }

    private void ExitFromTray()
    {
        _isExiting = true;
        Close();
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

    private static readonly Geometry MaximizeGeometry =
        Geometry.Parse("M 1.5,1.5 L 8.5,1.5 L 8.5,8.5 L 1.5,8.5 Z");

    private static readonly Geometry RestoreGeometry =
        Geometry.Parse("M 3,1.5 L 8.5,1.5 L 8.5,7 M 1.5,3 L 7,3 L 7,8.5 L 1.5,8.5 Z");

    private void UpdateMaximizeRestoreButton()
    {
        if (MaximizeRestoreIcon is null)
            return;

        MaximizeRestoreIcon.Data = WindowState == WindowState.Maximized
            ? RestoreGeometry
            : MaximizeGeometry;
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

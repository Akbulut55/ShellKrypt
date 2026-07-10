using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.ViewModels.QuickFill;

namespace ShellKrypt.Desktop.Views.QuickFill.Controls;

public partial class QuickFillKeyCapturePanel : UserControl
{
    public QuickFillKeyCapturePanel()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyCaptureKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickFillEntryEditorVm editor || !editor.CanCaptureQuickFillKey)
            return;

        if (!TryMapKey(e.Key, out var key))
            return;

        var modifiers = QuickFillKeyModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            modifiers |= QuickFillKeyModifiers.Ctrl;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            modifiers |= QuickFillKeyModifiers.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers |= QuickFillKeyModifiers.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            modifiers |= QuickFillKeyModifiers.Meta;

        editor.AddCapturedKeyStep(key, modifiers);
        e.Handled = true;
    }

    private static bool TryMapKey(Key key, out QuickFillKeystrokeKind kind)
    {
        kind = key switch
        {
            Key.Tab => QuickFillKeystrokeKind.Tab,
            Key.Enter => QuickFillKeystrokeKind.Enter,
            Key.Escape => QuickFillKeystrokeKind.Escape,
            Key.Space => QuickFillKeystrokeKind.Space,
            Key.Back => QuickFillKeystrokeKind.Backspace,
            Key.Delete => QuickFillKeystrokeKind.Delete,
            Key.Left => QuickFillKeystrokeKind.ArrowLeft,
            Key.Right => QuickFillKeystrokeKind.ArrowRight,
            Key.Up => QuickFillKeystrokeKind.ArrowUp,
            Key.Down => QuickFillKeystrokeKind.ArrowDown,
            Key.Home => QuickFillKeystrokeKind.Home,
            Key.End => QuickFillKeystrokeKind.End,
            Key.PageUp => QuickFillKeystrokeKind.PageUp,
            Key.PageDown => QuickFillKeystrokeKind.PageDown,
            Key.Insert => QuickFillKeystrokeKind.Insert,
            >= Key.F1 and <= Key.F12 => QuickFillKeystrokeKind.F1 + (key - Key.F1),
            >= Key.A and <= Key.Z => QuickFillKeystrokeKind.A + (key - Key.A),
            >= Key.D0 and <= Key.D9 => QuickFillKeystrokeKind.D0 + (key - Key.D0),
            _ => 0
        };

        return kind != 0;
    }
}

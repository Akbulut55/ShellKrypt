using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;

public partial class ApiKeyItemView : UserControl
{
    public static readonly StyledProperty<ICommand?> ShowDetailsCommandProperty = AvaloniaProperty.Register<ApiKeyItemView, ICommand?>(nameof(ShowDetailsCommand));
    public static readonly StyledProperty<ICommand?> ToggleSecretCommandProperty = AvaloniaProperty.Register<ApiKeyItemView, ICommand?>(nameof(ToggleSecretCommand));
    public static readonly StyledProperty<ICommand?> CopySecretCommandProperty = AvaloniaProperty.Register<ApiKeyItemView, ICommand?>(nameof(CopySecretCommand));

    public ApiKeyItemView() => InitializeComponent();

    public ICommand? ShowDetailsCommand { get => GetValue(ShowDetailsCommandProperty); set => SetValue(ShowDetailsCommandProperty, value); }
    public ICommand? ToggleSecretCommand { get => GetValue(ToggleSecretCommandProperty); set => SetValue(ToggleSecretCommandProperty, value); }
    public ICommand? CopySecretCommand { get => GetValue(CopySecretCommandProperty); set => SetValue(CopySecretCommandProperty, value); }

    private void Card_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsButtonSource(e.Source))
            return;

        if (ShowDetailsCommand?.CanExecute(DataContext) == true)
            ShowDetailsCommand.Execute(DataContext);
    }

    private static bool IsButtonSource(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button)
                return true;

        return false;
    }
}

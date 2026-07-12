using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemWorkspaceToolbar : UserControl
{
    public static readonly StyledProperty<string> SearchTextProperty = AvaloniaProperty.Register<ItemWorkspaceToolbar, string>(nameof(SearchText), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string> SearchPlaceholderProperty = AvaloniaProperty.Register<ItemWorkspaceToolbar, string>(nameof(SearchPlaceholder), "");
    public static readonly StyledProperty<object?> FilterContentProperty = AvaloniaProperty.Register<ItemWorkspaceToolbar, object?>(nameof(FilterContent));

    public ItemWorkspaceToolbar() => InitializeComponent();
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    public object? FilterContent { get => GetValue(FilterContentProperty); set => SetValue(FilterContentProperty, value); }
}

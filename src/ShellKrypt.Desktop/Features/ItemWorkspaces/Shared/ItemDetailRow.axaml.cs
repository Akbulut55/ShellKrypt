using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemDetailRow : UserControl
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ItemDetailRow, string>(nameof(Label), "");
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<ItemDetailRow, string>(nameof(Value), "");

    public ItemDetailRow() => InitializeComponent();

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

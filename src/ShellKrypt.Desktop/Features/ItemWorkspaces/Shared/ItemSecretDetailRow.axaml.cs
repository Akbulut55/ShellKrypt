using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemSecretDetailRow : UserControl
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ItemSecretDetailRow, string>(nameof(Label), "");
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<ItemSecretDetailRow, string>(nameof(Value), "");
    public static readonly StyledProperty<bool> IsRevealedProperty = AvaloniaProperty.Register<ItemSecretDetailRow, bool>(nameof(IsRevealed));
    public static readonly StyledProperty<bool> HasCopyActionProperty = AvaloniaProperty.Register<ItemSecretDetailRow, bool>(nameof(HasCopyAction), true);
    public static readonly StyledProperty<ICommand?> ToggleCommandProperty = AvaloniaProperty.Register<ItemSecretDetailRow, ICommand?>(nameof(ToggleCommand));
    public static readonly StyledProperty<ICommand?> CopyCommandProperty = AvaloniaProperty.Register<ItemSecretDetailRow, ICommand?>(nameof(CopyCommand));

    public ItemSecretDetailRow() => InitializeComponent();

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }
    public bool HasCopyAction { get => GetValue(HasCopyActionProperty); set => SetValue(HasCopyActionProperty, value); }
    public ICommand? ToggleCommand { get => GetValue(ToggleCommandProperty); set => SetValue(ToggleCommandProperty, value); }
    public ICommand? CopyCommand { get => GetValue(CopyCommandProperty); set => SetValue(CopyCommandProperty, value); }
}

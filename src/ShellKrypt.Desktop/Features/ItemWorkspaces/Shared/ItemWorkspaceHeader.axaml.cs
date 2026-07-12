using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemWorkspaceHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, string>(nameof(Title), "");
    public static readonly StyledProperty<string> SubtitleProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, string>(nameof(Subtitle), "");
    public static readonly StyledProperty<string> ResultTextProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, string>(nameof(ResultText), "");
    public static readonly StyledProperty<string> AddTextProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, string>(nameof(AddText), "");
    public static readonly StyledProperty<ICommand?> AddCommandProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, ICommand?>(nameof(AddCommand));
    public static readonly StyledProperty<object?> SummaryContentProperty = AvaloniaProperty.Register<ItemWorkspaceHeader, object?>(nameof(SummaryContent));

    public ItemWorkspaceHeader() => InitializeComponent();
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string ResultText { get => GetValue(ResultTextProperty); set => SetValue(ResultTextProperty, value); }
    public string AddText { get => GetValue(AddTextProperty); set => SetValue(AddTextProperty, value); }
    public ICommand? AddCommand { get => GetValue(AddCommandProperty); set => SetValue(AddCommandProperty, value); }
    public object? SummaryContent { get => GetValue(SummaryContentProperty); set => SetValue(SummaryContentProperty, value); }
}

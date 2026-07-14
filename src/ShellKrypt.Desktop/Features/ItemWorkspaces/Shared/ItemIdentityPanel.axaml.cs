using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemIdentityPanel : UserControl
{
    public static readonly StyledProperty<Geometry?> IconDataProperty = AvaloniaProperty.Register<ItemIdentityPanel, Geometry?>(nameof(IconData));
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ItemIdentityPanel, string>(nameof(Title), "");
    public static readonly StyledProperty<string> SubtitleProperty = AvaloniaProperty.Register<ItemIdentityPanel, string>(nameof(Subtitle), "");
    public static readonly StyledProperty<string> BadgeProperty = AvaloniaProperty.Register<ItemIdentityPanel, string>(nameof(Badge), "");
    public static readonly StyledProperty<string> EncryptionTextProperty = AvaloniaProperty.Register<ItemIdentityPanel, string>(nameof(EncryptionText), "");
    public static readonly StyledProperty<object?> BodyContentProperty = AvaloniaProperty.Register<ItemIdentityPanel, object?>(nameof(BodyContent));
    public static readonly StyledProperty<bool> ShowSummaryProperty = AvaloniaProperty.Register<ItemIdentityPanel, bool>(nameof(ShowSummary), true);
    public static readonly StyledProperty<bool> ShowEncryptionProperty = AvaloniaProperty.Register<ItemIdentityPanel, bool>(nameof(ShowEncryption), true);
    public static readonly StyledProperty<bool> HasBadgeProperty = AvaloniaProperty.Register<ItemIdentityPanel, bool>(nameof(HasBadge));

    public ItemIdentityPanel() => InitializeComponent();

    public Geometry? IconData { get => GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string EncryptionText { get => GetValue(EncryptionTextProperty); set => SetValue(EncryptionTextProperty, value); }
    public object? BodyContent { get => GetValue(BodyContentProperty); set => SetValue(BodyContentProperty, value); }
    public bool ShowSummary { get => GetValue(ShowSummaryProperty); set => SetValue(ShowSummaryProperty, value); }
    public bool ShowEncryption { get => GetValue(ShowEncryptionProperty); set => SetValue(ShowEncryptionProperty, value); }
    public bool HasBadge { get => GetValue(HasBadgeProperty); private set => SetValue(HasBadgeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BadgeProperty)
            HasBadge = !string.IsNullOrWhiteSpace(Badge);
    }
}

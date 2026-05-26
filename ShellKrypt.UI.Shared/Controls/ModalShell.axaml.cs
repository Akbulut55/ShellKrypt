using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShellKrypt.UI.Shared.Controls;

public partial class ModalShell : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalShell, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModalShell, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ModalShell, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<string> ErrorProperty =
        AvaloniaProperty.Register<ModalShell, string>(nameof(Error), string.Empty);

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ModalShell, bool>(nameof(HasError));

    public static readonly StyledProperty<string> FooterTextProperty =
        AvaloniaProperty.Register<ModalShell, string>(nameof(FooterText), string.Empty);

    public static readonly StyledProperty<string> CloseTextProperty =
        AvaloniaProperty.Register<ModalShell, string>(nameof(CloseText), "X");

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalShell, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<ModalShell, object?>(nameof(Body));

    public static readonly StyledProperty<object?> FooterActionsProperty =
        AvaloniaProperty.Register<ModalShell, object?>(nameof(FooterActions));

    public static readonly StyledProperty<double> DialogWidthProperty =
        AvaloniaProperty.Register<ModalShell, double>(nameof(DialogWidth), 640);

    public ModalShell()
    {
        InitializeComponent();
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        private set => SetValue(HasErrorProperty, value);
    }

    public string FooterText
    {
        get => GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    public string CloseText
    {
        get => GetValue(CloseTextProperty);
        set => SetValue(CloseTextProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public object? FooterActions
    {
        get => GetValue(FooterActionsProperty);
        set => SetValue(FooterActionsProperty, value);
    }

    public double DialogWidth
    {
        get => GetValue(DialogWidthProperty);
        set => SetValue(DialogWidthProperty, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ErrorProperty)
            HasError = !string.IsNullOrWhiteSpace(Error);
    }
}

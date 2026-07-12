using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public partial class ItemEditorFooter : UserControl
{
    public static readonly StyledProperty<bool> ShowCancelProperty = AvaloniaProperty.Register<ItemEditorFooter, bool>(nameof(ShowCancel));
    public static readonly StyledProperty<bool> ShowSaveProperty = AvaloniaProperty.Register<ItemEditorFooter, bool>(nameof(ShowSave));
    public static readonly StyledProperty<bool> ShowDetailsProperty = AvaloniaProperty.Register<ItemEditorFooter, bool>(nameof(ShowDetails), true);
    public static readonly StyledProperty<bool> ShowDeleteConfirmProperty = AvaloniaProperty.Register<ItemEditorFooter, bool>(nameof(ShowDeleteConfirm));
    public static readonly StyledProperty<ItemEditorMode> ModeProperty = AvaloniaProperty.Register<ItemEditorFooter, ItemEditorMode>(nameof(Mode));
    public static readonly StyledProperty<string> CancelTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(CancelText), "Cancel");
    public static readonly StyledProperty<string> SaveTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(SaveText), "Save");
    public static readonly StyledProperty<string> EditTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(EditText), "Edit");
    public static readonly StyledProperty<string> DeleteTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(DeleteText), "Delete");
    public static readonly StyledProperty<string> CloseTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(CloseText), "Close");
    public static readonly StyledProperty<string> InfoTextProperty = AvaloniaProperty.Register<ItemEditorFooter, string>(nameof(InfoText), "");
    public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(CancelCommand));
    public static readonly StyledProperty<ICommand?> SaveCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(SaveCommand));
    public static readonly StyledProperty<ICommand?> EditCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(EditCommand));
    public static readonly StyledProperty<ICommand?> DeleteCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(DeleteCommand));
    public static readonly StyledProperty<ICommand?> ConfirmDeleteCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(ConfirmDeleteCommand));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<ItemEditorFooter, ICommand?>(nameof(CloseCommand));

    public ItemEditorFooter()
    {
        InitializeComponent();
        UpdateModeState();
    }
    public ItemEditorMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }
    public string SaveText { get => GetValue(SaveTextProperty); set => SetValue(SaveTextProperty, value); }
    public string EditText { get => GetValue(EditTextProperty); set => SetValue(EditTextProperty, value); }
    public string DeleteText { get => GetValue(DeleteTextProperty); set => SetValue(DeleteTextProperty, value); }
    public string CloseText { get => GetValue(CloseTextProperty); set => SetValue(CloseTextProperty, value); }
    public string InfoText { get => GetValue(InfoTextProperty); set => SetValue(InfoTextProperty, value); }
    public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    public ICommand? SaveCommand { get => GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }
    public ICommand? EditCommand { get => GetValue(EditCommandProperty); set => SetValue(EditCommandProperty, value); }
    public ICommand? DeleteCommand { get => GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }
    public ICommand? ConfirmDeleteCommand { get => GetValue(ConfirmDeleteCommandProperty); set => SetValue(ConfirmDeleteCommandProperty, value); }
    public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public bool ShowCancel { get => GetValue(ShowCancelProperty); private set => SetValue(ShowCancelProperty, value); }
    public bool ShowSave { get => GetValue(ShowSaveProperty); private set => SetValue(ShowSaveProperty, value); }
    public bool ShowDetails { get => GetValue(ShowDetailsProperty); private set => SetValue(ShowDetailsProperty, value); }
    public bool ShowDeleteConfirm { get => GetValue(ShowDeleteConfirmProperty); private set => SetValue(ShowDeleteConfirmProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
            UpdateModeState();
    }

    private void UpdateModeState()
    {
        ShowCancel = Mode is ItemEditorMode.Add or ItemEditorMode.Edit or ItemEditorMode.ConfirmDelete;
        ShowSave = Mode is ItemEditorMode.Add or ItemEditorMode.Edit;
        ShowDetails = Mode == ItemEditorMode.Details;
        ShowDeleteConfirm = Mode == ItemEditorMode.ConfirmDelete;
    }
}

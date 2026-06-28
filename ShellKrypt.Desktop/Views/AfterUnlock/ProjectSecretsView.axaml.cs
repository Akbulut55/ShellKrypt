using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Views;

public partial class ProjectSecretsView : UserControl
{
    private static readonly DataFormat<string> VariableDragFormat = DataFormat.CreateStringApplicationFormat("shellkrypt-project-secret-variable");

    public ProjectSecretsView()
    {
        InitializeComponent();
    }

    private async void OnVariableDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ProjectSecretsViewModel { IsProjectEditing: true })
            return;

        if (sender is not Control { DataContext: ProjectSecretVariableRowVm variable })
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(VariableDragFormat, variable.Id));
        e.Handled = true;

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private void OnVariableRowDragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not ProjectSecretsViewModel { IsProjectEditing: true })
            return;

        if (!e.DataTransfer.Contains(VariableDragFormat))
            return;

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnVariableRowDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ProjectSecretsViewModel viewModel)
            return;

        if (sender is not Control { DataContext: ProjectSecretVariableRowVm target })
            return;

        var sourceId = e.DataTransfer.TryGetValue(VariableDragFormat);
        if (string.IsNullOrWhiteSpace(sourceId))
            return;

        var source = viewModel.Variables.FirstOrDefault(variable => variable.Id == sourceId);
        viewModel.MoveVariable(source, target);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }
}

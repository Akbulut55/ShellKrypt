using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class BackupOperationState : ViewModelBase
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "";

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    public async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Status = "";
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

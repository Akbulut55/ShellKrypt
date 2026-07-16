using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Features.Settings;

public sealed partial class SettingsViewModel
{
    private async Task RunSettingsOperationAsync(Func<Task> action)
    {
        try
        {
            Status = "";
            await action();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}

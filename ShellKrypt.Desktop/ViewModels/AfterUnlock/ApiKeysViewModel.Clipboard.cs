using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
    [RelayCommand]
    private async Task CopyFieldAsync(ApiKeyFieldRowVm? field)
    {
        Error = "";

        if (field is null || !field.IsCopyable || string.IsNullOrWhiteSpace(field.Value))
        {
            Error = "No copyable value is available.";
            return;
        }

        await _root.CopyToClipboardAsync(field.Value);
        _root.LogActivity("api_keys", "API key field copied", $"Copied {field.Label}.", "info", affectedItem: field.Label);
    }

    [RelayCommand]
    private async Task CopyPrimarySecretAsync(ApiKeyRowVm? row)
    {
        Error = "";

        if (row is null || string.IsNullOrWhiteSpace(row.PrimaryCopyValue))
        {
            Error = "No API key value is available to copy.";
            return;
        }

        await _root.CopyToClipboardAsync(row.PrimaryCopyValue);
        _root.LogActivity("api_keys", "API key copied", $"Copied {row.PrimaryFieldLabel} for {row.Name}.", "info", affectedItem: row.Name);
    }
}

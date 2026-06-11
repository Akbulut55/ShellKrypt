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
            Error = T(_root, "ApiKeys.Error.NoCopyableValue");
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
            Error = T(_root, "ApiKeys.Error.NoPrimaryValue");
            return;
        }

        await _root.CopyToClipboardAsync(row.PrimaryCopyValue);
        _root.LogActivity("api_keys", "API key copied", $"Copied {row.PrimaryFieldLabel} for {row.Name}.", "info", affectedItem: row.Name);
    }
}

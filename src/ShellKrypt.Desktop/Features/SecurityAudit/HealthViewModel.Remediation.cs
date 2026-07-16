using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Features.SecurityAudit;

public partial class HealthViewModel
{
    [RelayCommand]
    private async Task RemediateAsync(HealthIssueVm? issue)
    {
        Error = "";

        if (issue is null)
            return;

        if (!await RouteFindingAsync(issue))
            Error = T(_root, "SecurityAudit.Error.OpenAffectedItem");
    }

    private async Task<bool> RouteFindingAsync(HealthIssueVm issue)
    {
        switch (issue.RecommendedAction)
        {
            case HealthAuditRecommendedAction.GenerateReplacementPassword:
                return await _shell.ShowWebLoginForRemediationAsync(issue.ItemId, generateReplacementPassword: true);
            case HealthAuditRecommendedAction.OpenWebLogin:
                return await _shell.ShowWebLoginForRemediationAsync(issue.ItemId);
            case HealthAuditRecommendedAction.OpenCard:
                return await _shell.ShowCardByIdAsync(issue.ItemId);
            case HealthAuditRecommendedAction.OpenApiKey:
                return await _shell.ShowApiKeyByIdAsync(issue.ItemId);
            case HealthAuditRecommendedAction.OpenProjectSecret:
                return await _shell.ShowProjectSecretByIdAsync(issue.ItemId);
            case HealthAuditRecommendedAction.OpenSettings:
                _shell.ShowSettings();
                return true;
            default:
                return false;
        }
    }
}

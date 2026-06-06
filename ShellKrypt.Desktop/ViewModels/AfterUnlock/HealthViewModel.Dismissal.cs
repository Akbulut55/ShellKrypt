using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class HealthViewModel
{
    [RelayCommand]
    private void DismissSuggestion()
    {
        Error = "";

        var issue = PrimarySuggestionIssue;
        if (issue is null || string.IsNullOrWhiteSpace(issue.Fingerprint))
            return;

        _dismissedIssueStore.Dismiss(_root.VaultPath, issue.Fingerprint);
        _dismissedSuggestionFingerprints.Add(issue.Fingerprint);
        NotifySuggestionStateChanged();
        _root.LogActivity("audit", "Security suggestion dismissed", $"Dismissed {issue.Title} from Smart Suggestion.", "info", affectedItem: issue.AffectedItem);
    }
}

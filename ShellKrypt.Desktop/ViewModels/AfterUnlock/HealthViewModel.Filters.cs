using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class HealthViewModel
{
    [RelayCommand]
    private void ShowAllFindings() => ActiveFilter = FilterAll;

    [RelayCommand]
    private void ShowHighRiskFindings() => ActiveFilter = FilterHighRisk;

    [RelayCommand]
    private void ShowPasswordFindings() => ActiveFilter = FilterPasswords;

    [RelayCommand]
    private void ShowCardFindings() => ActiveFilter = FilterCards;

    [RelayCommand]
    private void ShowApiKeyFindings() => ActiveFilter = FilterApiKeys;

    [RelayCommand]
    private void ShowProjectSecretFindings() => ActiveFilter = FilterProjectSecrets;

    [RelayCommand]
    private void ShowSettingsFindings() => ActiveFilter = FilterSettings;

    private void RefreshVisibleIssues()
    {
        VisibleIssues.Clear();

        var visible = ActiveFilter switch
        {
            FilterHighRisk => _allIssues.Where(issue => issue.SeverityRank >= 3),
            FilterPasswords => _allIssues.Where(issue => issue.ScopeKey == FilterPasswords),
            FilterCards => _allIssues.Where(issue => issue.ScopeKey == FilterCards),
            FilterApiKeys => _allIssues.Where(issue => issue.ScopeKey == FilterApiKeys),
            FilterProjectSecrets => _allIssues.Where(issue => issue.ScopeKey == FilterProjectSecrets),
            FilterSettings => _allIssues.Where(issue => issue.ScopeKey == FilterSettings),
            _ => _allIssues
        };

        foreach (var issue in visible)
            VisibleIssues.Add(issue);
    }
}

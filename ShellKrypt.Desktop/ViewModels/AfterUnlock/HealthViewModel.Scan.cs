using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class HealthViewModel
{
    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _healthAuditService.AnalyzeAsync(_root.VaultPath, _root.VaultKey, BuildAuditOptions());
            _dismissedSuggestionFingerprints = new HashSet<string>(
                _dismissedIssueStore.LoadFingerprints(_root.VaultPath),
                StringComparer.OrdinalIgnoreCase);

            _allIssues.Clear();
            Issues.Clear();

            var displayOrder = 0;
            foreach (var issue in result.Issues)
            {
                var vm = new HealthIssueVm(issue, displayOrder++);
                _allIssues.Add(vm);
                Issues.Add(vm);
            }

            AnalyzedCount = result.AnalyzedCount;
            ReusedCount = result.ReusedCount;
            WeakCount = result.WeakCount;
            OldCount = result.OldCount;
            HighRiskCount = result.HighRiskCount;
            PasswordFindingCount = result.PasswordIssueCount;
            CardFindingCount = result.CardIssueCount;
            ApiKeyFindingCount = result.ApiKeyIssueCount;
            SettingsFindingCount = result.SettingsIssueCount;
            TotalIssueCount = result.Issues.Count;
            LastCheckedText = result.CheckedAtUtc.ToString("u");
            RefreshVisibleIssues();
            _root.LogActivity("audit", "Security audit refreshed", $"Reviewed {result.AnalyzedCount} vault records and session settings.", "info", affectedItem: "Security Audit");
            NotifyAuditStateChanged();
            NotifyFilterStateChanged();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private HealthAuditOptions BuildAuditOptions()
        => new(
            AutoLockEnabled: _root.AutoLockEnabled,
            LockOnDeactivate: _root.LockOnDeactivate,
            ClipboardClearSeconds: _root.ClipboardClearSeconds,
            ClipboardCopyEnabled: _root.ClipboardCopyEnabled);
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class HealthIssueVm : ObservableObject
{
    [ObservableProperty] private string itemId = "";
    [ObservableProperty] private string fingerprint = "";
    [ObservableProperty] private string severity = "";
    [ObservableProperty] private string category = "";
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string details = "";

    public string SeverityBadgeBackground => Severity switch
    {
        "CRITICAL" => "DangerMutedBrush",
        "HIGH" => "DangerMutedBrush",
        "MEDIUM" => "WarningMutedBrush",
        "LOW" => "InfoMutedBrush",
        _ => "InfoMutedBrush"
    };

    public string SeverityBadgeForeground => Severity switch
    {
        "CRITICAL" => "DangerBrush",
        "HIGH" => "DangerBrush",
        "MEDIUM" => "WarningForegroundBrush",
        "LOW" => "InfoBrush",
        _ => "InfoBrush"
    };

    public string SeverityAccentBrush => Severity switch
    {
        "CRITICAL" => "DangerBrush",
        "HIGH" => "DangerBrush",
        "MEDIUM" => "WarningBrush",
        "LOW" => "BorderBrushStrong",
        _ => "BorderBrushStrong"
    };

    public string IconGlyph => Category.ToUpperInvariant() switch
    {
        "BREACH" => "!!",
        "REUSED" => "RP",
        "WEAK" => "WP",
        "OLD" => "OC",
        _ => "!!"
    };
}

public partial class HealthViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IHealthAuditService _healthAuditService;
    private readonly DismissedAuditIssueStore _dismissedIssueStore = new();
    private HashSet<string> _dismissedSuggestionFingerprints = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<HealthIssueVm> Issues { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int analyzedCount;
    [ObservableProperty] private int reusedCount;
    [ObservableProperty] private int weakCount;
    [ObservableProperty] private int oldCount;
    [ObservableProperty] private int totalIssueCount;
    [ObservableProperty] private string lastCheckedText = "Never";

    public HealthViewModel(MainWindowViewModel root, ShellViewModel shell, IHealthAuditService healthAuditService)
    {
        _root = root;
        _shell = shell;
        _healthAuditService = healthAuditService;
        Issues.CollectionChanged += OnIssuesChanged;
        _ = RefreshAsync();
    }

    public string SummaryText => AnalyzedCount == 0
        ? "No web logins were found yet."
        : $"Analyzed {AnalyzedCount} web logins.";
    public string LastCheckedDisplay => $"Last checked: {LastCheckedText}";
    public bool HasIssues => Issues.Count > 0;
    public int HealthScore => Math.Clamp(100 - (ReusedCount * 2 + WeakCount * 2 + OldCount), 0, 100);
    public string HealthScoreDisplay => $"{HealthScore}%";
    public string HealthScoreTitle => HealthScore switch
    {
        >= 85 => "Vault is generally secure",
        >= 60 => "Vault needs attention",
        _ => "Immediate action required"
    };
    public string HealthScoreSubtitle => TotalIssueCount > 0
        ? $"{TotalIssueCount} vulnerabilities need immediate remediation"
        : "No active findings right now";
    public string SmartSuggestionTitle => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Title
        : Issues.Count > 0
            ? "All findings dismissed"
            : "No urgent finding";
    public string SmartSuggestionText => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Details
        : Issues.Count > 0
            ? "Dismissed findings stay hidden in this card until they change or a new issue appears."
            : "Run a scan after adding or editing credentials to generate remediation guidance.";
    public HealthIssueVm? PrimarySuggestionIssue => Issues.FirstOrDefault(issue =>
        !string.IsNullOrWhiteSpace(issue.Fingerprint) &&
        !_dismissedSuggestionFingerprints.Contains(issue.Fingerprint));
    public bool CanGenerateSuggestionPassword => PrimarySuggestionIssue is not null && !IsBusy;
    public bool CanDismissSuggestion => PrimarySuggestionIssue is not null && !IsBusy;
    public string ChecklistClipboardText => $"Clear clipboard ({Math.Max(SessionSecuritySettings.MinClipboardClearSeconds, _root.ClipboardClearSeconds)}s)";
    public bool HasClipboardTimeout => _root.ClipboardClearSeconds > 0;
    public bool HasAutoLock => _root.AutoLockEnabled;
    public bool HasFocusLock => _root.LockOnDeactivate;
    public string AuditStatusText => TotalIssueCount > 0
        ? "Lock down the highest-risk credentials first."
        : "No emergency mitigation required.";
    public string EmptyIssuesTitle => "No findings right now";
    public string EmptyIssuesText => "This vault does not currently have any weak, reused, or stale web login findings.";

    partial void OnAnalyzedCountChanged(int value)
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
        OnPropertyChanged(nameof(HealthScoreSubtitle));
    }

    partial void OnLastCheckedTextChanged(string value) => OnPropertyChanged(nameof(LastCheckedDisplay));

    partial void OnReusedCountChanged(int value)
    {
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
    }

    partial void OnWeakCountChanged(int value)
    {
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
    }

    partial void OnOldCountChanged(int value)
    {
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
    }

    partial void OnTotalIssueCountChanged(int value)
    {
        OnPropertyChanged(nameof(HealthScoreSubtitle));
        OnPropertyChanged(nameof(SmartSuggestionTitle));
        OnPropertyChanged(nameof(SmartSuggestionText));
        OnPropertyChanged(nameof(AuditStatusText));
        OnPropertyChanged(nameof(EmptyIssuesTitle));
        OnPropertyChanged(nameof(EmptyIssuesText));
        OnPropertyChanged(nameof(PrimarySuggestionIssue));
        OnPropertyChanged(nameof(CanGenerateSuggestionPassword));
        OnPropertyChanged(nameof(CanDismissSuggestion));
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task RemediateAsync(HealthIssueVm? issue)
    {
        Error = "";

        if (issue is null || string.IsNullOrWhiteSpace(issue.ItemId))
            return;

        if (!await _shell.ShowWebLoginForRemediationAsync(issue.ItemId))
            Error = "The affected login could not be opened.";
    }

    [RelayCommand]
    private async Task GenerateUniqueBatchAsync()
    {
        Error = "";

        var issue = PrimarySuggestionIssue;
        if (issue is null || string.IsNullOrWhiteSpace(issue.ItemId))
            return;

        if (!await _shell.ShowWebLoginForRemediationAsync(issue.ItemId, generateReplacementPassword: true))
            Error = "A replacement password could not be prepared.";
    }

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
        _root.LogActivity("audit", "Security suggestion dismissed", $"Dismissed {issue.Title} from Smart Suggestion.", "info", affectedItem: issue.Title);
    }

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
            var result = await _healthAuditService.AnalyzeAsync(_root.VaultPath, _root.VaultKey);
            _dismissedSuggestionFingerprints = new HashSet<string>(
                _dismissedIssueStore.LoadFingerprints(_root.VaultPath),
                StringComparer.OrdinalIgnoreCase);

            Issues.Clear();
            foreach (var issue in result.Issues)
            {
                var fingerprint = BuildFingerprint(issue);
                Issues.Add(new HealthIssueVm
                {
                    ItemId = issue.ItemId,
                    Fingerprint = fingerprint,
                    Severity = issue.Severity,
                    Category = issue.Category,
                    Title = issue.Title,
                    Details = issue.Details
                });
            }

            AnalyzedCount = result.AnalyzedCount;
            ReusedCount = result.ReusedCount;
            WeakCount = result.WeakCount;
            OldCount = result.OldCount;
            TotalIssueCount = result.Issues.Count;
            LastCheckedText = result.CheckedAtUtc.ToString("u");
            _root.LogActivity("audit", "Security audit refreshed", $"Reviewed {result.AnalyzedCount} web login records.", "info", affectedItem: "Security Audit");
            OnPropertyChanged(nameof(HasIssues));
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

    private void OnIssuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HealthScoreSubtitle));
        OnPropertyChanged(nameof(AuditStatusText));
        OnPropertyChanged(nameof(EmptyIssuesTitle));
        OnPropertyChanged(nameof(EmptyIssuesText));
        NotifySuggestionStateChanged();
    }

    private void NotifySuggestionStateChanged()
    {
        OnPropertyChanged(nameof(SmartSuggestionTitle));
        OnPropertyChanged(nameof(SmartSuggestionText));
        OnPropertyChanged(nameof(PrimarySuggestionIssue));
        OnPropertyChanged(nameof(CanGenerateSuggestionPassword));
        OnPropertyChanged(nameof(CanDismissSuggestion));
    }

    private static string BuildFingerprint(HealthAuditIssue issue)
    {
        var raw = string.Join("|",
            issue.ItemId.Trim(),
            issue.Severity.Trim().ToUpperInvariant(),
            issue.Category.Trim().ToUpperInvariant(),
            issue.Title.Trim(),
            issue.Details.Trim());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}

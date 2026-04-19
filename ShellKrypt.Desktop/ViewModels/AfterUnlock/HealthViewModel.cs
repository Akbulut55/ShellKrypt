using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class HealthIssueVm : ObservableObject
{
    [ObservableProperty] private string severity = "";
    [ObservableProperty] private string category = "";
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string details = "";

    public string SeverityBadgeBackground => Severity switch
    {
        "CRITICAL" => "#93000a",
        "HIGH" => "#93000a",
        "MEDIUM" => "#744000",
        "LOW" => "#353534",
        _ => "#353534"
    };

    public string SeverityBadgeForeground => Severity switch
    {
        "CRITICAL" => "#ffdad6",
        "HIGH" => "#ffdad6",
        "MEDIUM" => "#ffd1aa",
        "LOW" => "#bacac5",
        _ => "#bacac5"
    };

    public string SeverityAccentBrush => Severity switch
    {
        "CRITICAL" => "#ffb4ab",
        "HIGH" => "#ffb4ab",
        "MEDIUM" => "#ffac5a",
        "LOW" => "#859490",
        _ => "#859490"
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
    private readonly IHealthAuditService _healthAuditService;

    public ObservableCollection<HealthIssueVm> Issues { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int analyzedCount;
    [ObservableProperty] private int reusedCount;
    [ObservableProperty] private int weakCount;
    [ObservableProperty] private int oldCount;
    [ObservableProperty] private string lastCheckedText = "Never";

    public HealthViewModel(MainWindowViewModel root, IHealthAuditService healthAuditService)
    {
        _root = root;
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
    public string HealthScoreSubtitle => HasIssues
        ? $"{Issues.Count} vulnerabilities need immediate remediation"
        : "No active findings right now";
    public string MasterPasswordSummary => AnalyzedCount == 0
        ? "Run a scan to generate vault password hygiene context."
        : $"Checked {LastCheckedText} • Password hygiene snapshot";
    public string SmartSuggestionTitle => Issues.Count == 0 ? "No urgent finding" : Issues[0].Title;
    public string SmartSuggestionText => Issues.Count == 0
        ? "Run a scan after adding or editing credentials to generate remediation guidance."
        : Issues[0].Details;
    public string ChecklistClipboardText => $"Clear clipboard ({Math.Max(1, _root.ClipboardClearSeconds)}s)";
    public bool HasClipboardTimeout => _root.ClipboardClearSeconds > 0;
    public bool HasAutoLock => _root.AutoLockEnabled;
    public bool HasFocusLock => _root.LockOnDeactivate;
    public string AuditStatusText => HasIssues
        ? "Lock down the highest-risk credentials first."
        : "No emergency mitigation required.";

    partial void OnAnalyzedCountChanged(int value)
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
        OnPropertyChanged(nameof(HealthScoreSubtitle));
        OnPropertyChanged(nameof(MasterPasswordSummary));
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
            var result = await _healthAuditService.AnalyzeAsync(_root.VaultPath, _root.VaultKey);

            Issues.Clear();
            foreach (var issue in result.Issues)
            {
                Issues.Add(new HealthIssueVm
                {
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
            LastCheckedText = result.CheckedAtUtc.ToString("u");
            _root.LogActivity("settings", "Security audit refreshed", $"Reviewed {result.AnalyzedCount} web login records.", "info");
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
        OnPropertyChanged(nameof(SmartSuggestionTitle));
        OnPropertyChanged(nameof(SmartSuggestionText));
        OnPropertyChanged(nameof(AuditStatusText));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Settings;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class HealthViewModel : ViewModelBase
{
    internal const string FilterAll = "all";
    internal const string FilterHighRisk = "high";
    internal const string FilterPasswords = "passwords";
    internal const string FilterCards = "cards";
    internal const string FilterApiKeys = "api";
    internal const string FilterSettings = "settings";

    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IHealthAuditService _healthAuditService;
    private readonly AuditDismissalService _dismissedIssueStore;
    private readonly List<HealthIssueVm> _allIssues = new();
    private HashSet<string> _dismissedSuggestionFingerprints = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<HealthIssueVm> Issues { get; } = new();
    public ObservableCollection<HealthIssueVm> VisibleIssues { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int analyzedCount;
    [ObservableProperty] private int reusedCount;
    [ObservableProperty] private int weakCount;
    [ObservableProperty] private int oldCount;
    [ObservableProperty] private int highRiskCount;
    [ObservableProperty] private int passwordFindingCount;
    [ObservableProperty] private int cardFindingCount;
    [ObservableProperty] private int apiKeyFindingCount;
    [ObservableProperty] private int settingsFindingCount;
    [ObservableProperty] private int totalIssueCount;
    [ObservableProperty] private string lastCheckedText = "Never";
    [ObservableProperty] private string activeFilter = FilterAll;

    public HealthViewModel(
        MainWindowViewModel root,
        ShellViewModel shell,
        IHealthAuditService healthAuditService,
        AuditDismissalService dismissedIssueStore)
    {
        _root = root;
        _shell = shell;
        _healthAuditService = healthAuditService;
        _dismissedIssueStore = dismissedIssueStore;
        Issues.CollectionChanged += OnIssuesChanged;
        VisibleIssues.CollectionChanged += OnVisibleIssuesChanged;
        _ = RefreshAsync();
    }

    public string SummaryText => AnalyzedCount == 0
        ? "Scanned session settings. Add vault records to expand the local audit."
        : $"Scanned {AnalyzedCount} vault records and session settings.";
    public string LastCheckedDisplay => $"Last checked: {LastCheckedText}";
    public bool HasIssues => Issues.Count > 0;
    public bool HasVisibleIssues => VisibleIssues.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int OtherFindingCount => CardFindingCount + ApiKeyFindingCount + SettingsFindingCount;
    public string HealthScoreDisplay => $"{HealthScore}%";
    public string HealthScoreTitle => HealthScore switch
    {
        >= 85 => "Vault posture looks good",
        >= 60 => "Vault needs attention",
        _ => "Immediate action required"
    };
    public string HealthScoreSubtitle => TotalIssueCount > 0
        ? $"{TotalIssueCount} local findings need review"
        : "No local findings right now";
    public string SmartSuggestionTitle => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Title
        : Issues.Count > 0
            ? "All suggestions dismissed"
            : "No urgent finding";
    public string SmartSuggestionText => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Details
        : Issues.Count > 0
            ? "Dismissed suggestions stay hidden until the finding changes or a new issue appears."
            : "Run a scan after adding or editing vault records to generate local guidance.";
    public string SmartSuggestionActionText => PrimarySuggestionIssue?.ActionText ?? "Review";
    public HealthIssueVm? PrimarySuggestionIssue => _allIssues
        .Where(issue => !string.IsNullOrWhiteSpace(issue.Fingerprint) &&
                        !_dismissedSuggestionFingerprints.Contains(issue.Fingerprint))
        .OrderByDescending(issue => issue.SeverityRank)
        .ThenBy(issue => issue.DisplayOrder)
        .FirstOrDefault();
    public bool CanRunSmartSuggestion => PrimarySuggestionIssue is not null && !IsBusy;
    public bool CanGenerateSuggestionPassword => PrimarySuggestionIssue?.RecommendedAction == HealthAuditRecommendedAction.GenerateReplacementPassword && !IsBusy;
    public bool CanDismissSuggestion => PrimarySuggestionIssue is not null && !IsBusy;
    public string ChecklistClipboardText => $"Clear clipboard ({Math.Max(SessionSecuritySettings.MinClipboardClearSeconds, _root.ClipboardClearSeconds)}s)";
    public bool HasClipboardTimeout => _root.ClipboardClearSeconds > 0;
    public bool HasAutoLock => _root.AutoLockEnabled;
    public bool HasFocusLock => _root.LockOnDeactivate;
    public string AuditStatusText => HighRiskCount > 0
        ? "Fix high-risk findings first, then review medium and low-risk guidance."
        : TotalIssueCount > 0
            ? "Review local findings and tighten settings where practical."
            : "No local security findings were found.";
    public string EmptyIssuesTitle => ActiveFilter == FilterAll
        ? "No local findings right now"
        : "No findings match this filter";
    public string EmptyIssuesText => ActiveFilter == FilterAll
        ? "ShellKrypt scanned web logins, cards, API keys, and session settings without finding local issues."
        : "Try another audit filter or run a new scan after changing vault records.";
    public bool IsAllFilterActive => ActiveFilter == FilterAll;
    public bool IsHighRiskFilterActive => ActiveFilter == FilterHighRisk;
    public bool IsPasswordFilterActive => ActiveFilter == FilterPasswords;
    public bool IsCardFilterActive => ActiveFilter == FilterCards;
    public bool IsApiKeyFilterActive => ActiveFilter == FilterApiKeys;
    public bool IsSettingsFilterActive => ActiveFilter == FilterSettings;

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnAnalyzedCountChanged(int value) => NotifyAuditStateChanged();
    partial void OnLastCheckedTextChanged(string value) => OnPropertyChanged(nameof(LastCheckedDisplay));
    partial void OnReusedCountChanged(int value) => NotifyScoreChanged();
    partial void OnWeakCountChanged(int value) => NotifyScoreChanged();
    partial void OnOldCountChanged(int value) => NotifyScoreChanged();
    partial void OnHighRiskCountChanged(int value) => NotifyScoreChanged();
    partial void OnPasswordFindingCountChanged(int value) => NotifyAuditStateChanged();
    partial void OnCardFindingCountChanged(int value) => NotifyAuditStateChanged();
    partial void OnApiKeyFindingCountChanged(int value) => NotifyAuditStateChanged();
    partial void OnSettingsFindingCountChanged(int value) => NotifyAuditStateChanged();
    partial void OnTotalIssueCountChanged(int value) => NotifyAuditStateChanged();

    partial void OnActiveFilterChanged(string value)
    {
        RefreshVisibleIssues();
        NotifyFilterStateChanged();
    }

    private void OnIssuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasIssues));
        NotifySuggestionStateChanged();
    }

    private void OnVisibleIssuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasVisibleIssues));
        OnPropertyChanged(nameof(EmptyIssuesTitle));
        OnPropertyChanged(nameof(EmptyIssuesText));
    }

    private void NotifyAuditStateChanged()
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(OtherFindingCount));
        NotifyScoreChanged();
        NotifySuggestionStateChanged();
    }

    private void NotifyScoreChanged()
    {
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
        OnPropertyChanged(nameof(HealthScoreSubtitle));
        OnPropertyChanged(nameof(AuditStatusText));
    }

    private void NotifySuggestionStateChanged()
    {
        OnPropertyChanged(nameof(SmartSuggestionTitle));
        OnPropertyChanged(nameof(SmartSuggestionText));
        OnPropertyChanged(nameof(SmartSuggestionActionText));
        OnPropertyChanged(nameof(PrimarySuggestionIssue));
        OnPropertyChanged(nameof(CanRunSmartSuggestion));
        OnPropertyChanged(nameof(CanGenerateSuggestionPassword));
        OnPropertyChanged(nameof(CanDismissSuggestion));
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsHighRiskFilterActive));
        OnPropertyChanged(nameof(IsPasswordFilterActive));
        OnPropertyChanged(nameof(IsCardFilterActive));
        OnPropertyChanged(nameof(IsApiKeyFilterActive));
        OnPropertyChanged(nameof(IsSettingsFilterActive));
        OnPropertyChanged(nameof(EmptyIssuesTitle));
        OnPropertyChanged(nameof(EmptyIssuesText));
    }
}

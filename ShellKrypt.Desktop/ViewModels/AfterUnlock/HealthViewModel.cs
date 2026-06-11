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
        ? T(_root, "SecurityAudit.Summary.NoRecords")
        : T(_root, "SecurityAudit.Summary.Records", AnalyzedCount);
    public string LastCheckedDisplay => T(_root, "SecurityAudit.LastChecked", LastCheckedText);
    public bool HasIssues => Issues.Count > 0;
    public bool HasVisibleIssues => VisibleIssues.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int OtherFindingCount => CardFindingCount + ApiKeyFindingCount + SettingsFindingCount;
    public string HealthScoreDisplay => $"{HealthScore}%";
    public string HealthScoreTitle => HealthScore switch
    {
        >= 85 => T(_root, "SecurityAudit.Score.Good"),
        >= 60 => T(_root, "SecurityAudit.Score.Attention"),
        _ => T(_root, "SecurityAudit.Score.Immediate")
    };
    public string HealthScoreSubtitle => TotalIssueCount > 0
        ? T(_root, "SecurityAudit.Score.Findings", TotalIssueCount)
        : T(_root, "SecurityAudit.Score.NoFindings");
    public string SmartSuggestionTitle => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Title
        : Issues.Count > 0
            ? T(_root, "SecurityAudit.Suggestion.AllDismissed")
            : T(_root, "SecurityAudit.Suggestion.NoUrgent");
    public string SmartSuggestionText => PrimarySuggestionIssue is not null
        ? PrimarySuggestionIssue.Details
        : Issues.Count > 0
            ? T(_root, "SecurityAudit.Suggestion.DismissedText")
            : T(_root, "SecurityAudit.Suggestion.EmptyText");
    public string SmartSuggestionActionText => PrimarySuggestionIssue?.ActionText ?? T(_root, "Common.Review");
    public HealthIssueVm? PrimarySuggestionIssue => _allIssues
        .Where(issue => !string.IsNullOrWhiteSpace(issue.Fingerprint) &&
                        !_dismissedSuggestionFingerprints.Contains(issue.Fingerprint))
        .OrderByDescending(issue => issue.SeverityRank)
        .ThenBy(issue => issue.DisplayOrder)
        .FirstOrDefault();
    public bool CanRunSmartSuggestion => PrimarySuggestionIssue is not null && !IsBusy;
    public bool CanGenerateSuggestionPassword => PrimarySuggestionIssue?.RecommendedAction == HealthAuditRecommendedAction.GenerateReplacementPassword && !IsBusy;
    public bool CanDismissSuggestion => PrimarySuggestionIssue is not null && !IsBusy;
    public string ChecklistClipboardText => T(_root, "SecurityAudit.Checklist.Clipboard", Math.Max(SessionSecuritySettings.MinClipboardClearSeconds, _root.ClipboardClearSeconds));
    public bool HasClipboardTimeout => _root.ClipboardClearSeconds > 0;
    public bool HasAutoLock => _root.AutoLockEnabled;
    public bool HasFocusLock => _root.LockOnDeactivate;
    public string AuditStatusText => HighRiskCount > 0
        ? T(_root, "SecurityAudit.Status.HighRisk", HighRiskCount)
        : TotalIssueCount > 0
            ? T(_root, "SecurityAudit.Status.Review", TotalIssueCount)
            : T(_root, "SecurityAudit.Status.Clean");
    public string EmptyIssuesTitle => ActiveFilter == FilterAll
        ? T(_root, "SecurityAudit.Empty.NoFindingsTitle")
        : T(_root, "SecurityAudit.Empty.NoFilterTitle");
    public string EmptyIssuesText => ActiveFilter == FilterAll
        ? T(_root, "SecurityAudit.Empty.NoFindingsText")
        : T(_root, "SecurityAudit.Empty.NoFilterText");
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
        OnPropertyChanged(nameof(AllFilterLabel));
        OnPropertyChanged(nameof(HighRiskFilterLabel));
        OnPropertyChanged(nameof(PasswordsFilterLabel));
        OnPropertyChanged(nameof(CardsFilterLabel));
        OnPropertyChanged(nameof(ApiKeysFilterLabel));
        OnPropertyChanged(nameof(SettingsFilterLabel));
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

    public string AllFilterLabel => T(_root, "SecurityAudit.Filter.All", TotalIssueCount);
    public string HighRiskFilterLabel => T(_root, "SecurityAudit.Filter.HighRisk", HighRiskCount);
    public string PasswordsFilterLabel => T(_root, "SecurityAudit.Filter.Passwords", PasswordFindingCount);
    public string CardsFilterLabel => T(_root, "SecurityAudit.Filter.Cards", CardFindingCount);
    public string ApiKeysFilterLabel => T(_root, "SecurityAudit.Filter.ApiKeys", ApiKeyFindingCount);
    public string SettingsFilterLabel => T(_root, "SecurityAudit.Filter.Settings", SettingsFindingCount);

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(SummaryText),
            nameof(LastCheckedDisplay),
            nameof(HealthScoreTitle),
            nameof(HealthScoreSubtitle),
            nameof(SmartSuggestionTitle),
            nameof(SmartSuggestionText),
            nameof(SmartSuggestionActionText),
            nameof(ChecklistClipboardText),
            nameof(AuditStatusText),
            nameof(EmptyIssuesTitle),
            nameof(EmptyIssuesText),
            nameof(AllFilterLabel),
            nameof(HighRiskFilterLabel),
            nameof(PasswordsFilterLabel),
            nameof(CardsFilterLabel),
            nameof(ApiKeysFilterLabel),
            nameof(SettingsFilterLabel));
    }
}

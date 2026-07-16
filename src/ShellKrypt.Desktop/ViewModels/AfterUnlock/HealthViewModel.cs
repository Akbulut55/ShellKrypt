using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Audit;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.ViewModels;

public partial class HealthViewModel : ViewModelBase
{
    internal const string FilterAll = "all";
    internal const string FilterHighRisk = "high";
    internal const string FilterPasswords = "passwords";
    internal const string FilterCards = "cards";
    internal const string FilterApiKeys = "api";
    internal const string FilterProjectSecrets = "project";
    internal const string FilterSettings = "settings";

    private readonly DesktopFeatureServices _root;
    private readonly ShellViewModel _shell;
    private readonly IHealthAuditService _healthAuditService;
    private readonly List<HealthIssueVm> _allIssues = new();

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
    [ObservableProperty] private int projectSecretFindingCount;
    [ObservableProperty] private int settingsFindingCount;
    [ObservableProperty] private int totalIssueCount;
    [ObservableProperty] private string lastCheckedText = "Never";
    [ObservableProperty] private string activeFilter = FilterAll;

    public HealthViewModel(
        DesktopFeatureServices root,
        ShellViewModel shell,
        IHealthAuditService healthAuditService)
    {
        _root = root;
        _shell = shell;
        _healthAuditService = healthAuditService;
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
    public int OtherFindingCount => CardFindingCount + ApiKeyFindingCount + ProjectSecretFindingCount + SettingsFindingCount;
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
    public bool IsProjectSecretFilterActive => ActiveFilter == FilterProjectSecrets;
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
    partial void OnProjectSecretFindingCountChanged(int value) => NotifyAuditStateChanged();
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
        OnPropertyChanged(nameof(ProjectSecretsFilterLabel));
        OnPropertyChanged(nameof(SettingsFilterLabel));
        NotifyScoreChanged();
    }

    private void NotifyScoreChanged()
    {
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreTitle));
        OnPropertyChanged(nameof(HealthScoreSubtitle));
        OnPropertyChanged(nameof(AuditStatusText));
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsHighRiskFilterActive));
        OnPropertyChanged(nameof(IsPasswordFilterActive));
        OnPropertyChanged(nameof(IsCardFilterActive));
        OnPropertyChanged(nameof(IsApiKeyFilterActive));
        OnPropertyChanged(nameof(IsProjectSecretFilterActive));
        OnPropertyChanged(nameof(IsSettingsFilterActive));
        OnPropertyChanged(nameof(EmptyIssuesTitle));
        OnPropertyChanged(nameof(EmptyIssuesText));
    }

    public string AllFilterLabel => T(_root, "SecurityAudit.Filter.All", TotalIssueCount);
    public string HighRiskFilterLabel => T(_root, "SecurityAudit.Filter.HighRisk", HighRiskCount);
    public string PasswordsFilterLabel => T(_root, "SecurityAudit.Filter.Passwords", PasswordFindingCount);
    public string CardsFilterLabel => T(_root, "SecurityAudit.Filter.Cards", CardFindingCount);
    public string ApiKeysFilterLabel => T(_root, "SecurityAudit.Filter.ApiKeys", ApiKeyFindingCount);
    public string ProjectSecretsFilterLabel => T(_root, "SecurityAudit.Filter.ProjectSecrets", ProjectSecretFindingCount);
    public string SettingsFilterLabel => T(_root, "SecurityAudit.Filter.Settings", SettingsFindingCount);

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(SummaryText),
            nameof(LastCheckedDisplay),
            nameof(HealthScoreTitle),
            nameof(HealthScoreSubtitle),
            nameof(AuditStatusText),
            nameof(EmptyIssuesTitle),
            nameof(EmptyIssuesText),
            nameof(AllFilterLabel),
            nameof(HighRiskFilterLabel),
            nameof(PasswordsFilterLabel),
            nameof(CardsFilterLabel),
            nameof(ApiKeysFilterLabel),
            nameof(ProjectSecretsFilterLabel),
            nameof(SettingsFilterLabel));
    }
}

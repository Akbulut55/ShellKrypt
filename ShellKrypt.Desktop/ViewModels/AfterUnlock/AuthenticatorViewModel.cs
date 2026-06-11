using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel : ViewModelBase
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RecentlyUsedWindow = TimeSpan.FromHours(24);

    private readonly MainWindowViewModel _root;
    private readonly IAuthenticatorService _authenticatorService;
    private readonly AuthenticatorQrImportService _qrImportService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<AuthenticatorAccountVm> _allEntries = new();

    private long _formCounter;

    public ObservableCollection<AuthenticatorAccountVm> FilteredEntries { get; } = new();
    public ObservableCollection<AuthenticatorKeyTypeOption> KeyTypeOptions { get; } = new()
    {
        new(AuthenticatorKeyType.TimeBased, "Time Based"),
        new(AuthenticatorKeyType.CounterBased, "Counter Based")
    };
    public ObservableCollection<AuthenticatorAlgorithmOption> AlgorithmOptions { get; } = new()
    {
        new("HMAC-SHA1", "SHA1 algorithm (Default)", "SHA1"),
        new("HMAC-SHA256", "SHA256 algorithm", "SHA256"),
        new("HMAC-SHA512", "SHA512 algorithm", "SHA512")
    };
    public ObservableCollection<AuthenticatorDigitsOption> DigitsOptions { get; } = new()
    {
        new(6, "6 digits (Default)"),
        new(8, "8 digits")
    };

    [ObservableProperty] private AuthenticatorAccountVm? selectedEntry;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string error = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEditorModalOpen;
    [ObservableProperty] private bool isDetailsModalOpen;
    [ObservableProperty] private bool isEditingExisting;
    [ObservableProperty] private bool isDeleteConfirmOpen;
    [ObservableProperty] private bool isFormSecretVisible;
    [ObservableProperty] private bool isAdvancedOptionsExpanded;
    [ObservableProperty] private string formName = string.Empty;
    [ObservableProperty] private string formSecret = string.Empty;
    [ObservableProperty] private string formPeriodSecondsText = "30";
    [ObservableProperty] private AuthenticatorKeyTypeOption? selectedFormKeyType;
    [ObservableProperty] private AuthenticatorAlgorithmOption? selectedFormAlgorithm;
    [ObservableProperty] private AuthenticatorDigitsOption? selectedFormDigits;

    public AuthenticatorViewModel(
        MainWindowViewModel root,
        IAuthenticatorService authenticatorService,
        AuthenticatorQrImportService qrImportService,
        Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _authenticatorService = authenticatorService;
        _qrImportService = qrImportService;
        _refreshAllItemsAsync = refreshAllItemsAsync;

        SelectedFormKeyType = KeyTypeOptions[0];
        SelectedFormAlgorithm = AlgorithmOptions[0];
        SelectedFormDigits = DigitsOptions[0];

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += (_, _) => RefreshSnapshots();
        _refreshTimer.Start();

        _ = LoadAsync();
    }

    public int TotalCount => _allEntries.Count;
    public string CodesCountDisplay => T(_root, "Authenticator.Codes.Count", TotalCount);
    public int RefreshingSoonCount => _allEntries.Count(entry => entry.HasCountdown && entry.IsCodeValid && entry.SecondsRemaining <= 5);
    public int RecentlyUsedCount => _allEntries.Count(entry => IsRecentlyUsed(entry.LastUsedAtUtc));
    public bool HasEntries => FilteredEntries.Count > 0;
    public bool HasSelection => SelectedEntry is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanEditSelection => SelectedEntry is not null && !IsBusy;
    public bool CanCopyCode => SelectedEntry?.IsCodeValid == true && !IsBusy;
    public string PageSubtitle => T(_root, "Authenticator.Subtitle");
    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Authenticator.Empty.NoneTitle")
        : T(_root, "Authenticator.Empty.NoMatchTitle");
    public string EmptySubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Authenticator.Empty.NoneSubtitle")
        : T(_root, "Authenticator.Empty.NoMatchSubtitle");
    public string DetailSubtitle => SelectedEntry is null
        ? T(_root, "Authenticator.Detail.SelectCode")
        : SelectedEntry.KeyTypeDisplay;
    public string EditorModalTitle => IsEditingExisting ? T(_root, "Authenticator.Modal.EditTitle") : T(_root, "Authenticator.Modal.AddTitle");
    public string EditorModalSubtitle => T(_root, "Authenticator.Modal.Subtitle");
    public string AdvancedOptionsNote => T(_root, "Authenticator.Advanced.Note");
    public string SaveButtonText => IsEditingExisting ? T(_root, "Common.SaveChanges") : T(_root, "Authenticator.Button.AddCode");
    public string FormSecretVisibilityText => IsFormSecretVisible ? T(_root, "Common.Hide") : T(_root, "Common.Show");
    public string DeleteConfirmationText => SelectedEntry is null
        ? T(_root, "Authenticator.Delete.TitleFallback")
        : T(_root, "Authenticator.Delete.Title", SelectedEntry.Name);
    public string SelectedTypeSummary => SelectedFormKeyType?.KeyType == AuthenticatorKeyType.CounterBased
        ? T(_root, "Authenticator.TypeSummary.Counter", _formCounter, SelectedFormAlgorithm?.ShortLabel ?? "SHA1", SelectedFormDigits?.Digits ?? 6)
        : T(_root, "Authenticator.TypeSummary.Time", NormalizePeriodText(FormPeriodSecondsText), SelectedFormAlgorithm?.ShortLabel ?? "SHA1", SelectedFormDigits?.Digits ?? 6);
    public bool ShowAdvancedPeriod => SelectedFormKeyType?.KeyType == AuthenticatorKeyType.TimeBased;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedEntryChanged(AuthenticatorAccountVm? value)
    {
        foreach (var entry in FilteredEntries)
            entry.IsSelected = ReferenceEquals(entry, value);

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(CanCopyCode));
        OnPropertyChanged(nameof(DetailSubtitle));
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(CanCopyCode));
    }

    partial void OnIsEditingExistingChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorModalTitle));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    partial void OnSelectedFormKeyTypeChanged(AuthenticatorKeyTypeOption? value)
    {
        OnPropertyChanged(nameof(SelectedTypeSummary));
        OnPropertyChanged(nameof(ShowAdvancedPeriod));
    }

    partial void OnSelectedFormAlgorithmChanged(AuthenticatorAlgorithmOption? value)
        => OnPropertyChanged(nameof(SelectedTypeSummary));

    partial void OnSelectedFormDigitsChanged(AuthenticatorDigitsOption? value)
        => OnPropertyChanged(nameof(SelectedTypeSummary));

    partial void OnFormPeriodSecondsTextChanged(string value)
        => OnPropertyChanged(nameof(SelectedTypeSummary));

    partial void OnIsFormSecretVisibleChanged(bool value)
        => OnPropertyChanged(nameof(FormSecretVisibilityText));

    public override void RefreshLocalization()
    {
        foreach (var entry in _allEntries)
            entry.RefreshLocalization();

        NotifyLocalized(
            nameof(PageSubtitle),
            nameof(CodesCountDisplay),
            nameof(EmptyTitle),
            nameof(EmptySubtitle),
            nameof(DetailSubtitle),
            nameof(EditorModalTitle),
            nameof(EditorModalSubtitle),
            nameof(AdvancedOptionsNote),
            nameof(SaveButtonText),
            nameof(FormSecretVisibilityText),
            nameof(DeleteConfirmationText),
            nameof(SelectedTypeSummary));
    }
}

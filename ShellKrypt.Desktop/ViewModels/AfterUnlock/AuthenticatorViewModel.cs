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
    public int RefreshingSoonCount => _allEntries.Count(entry => entry.HasCountdown && entry.IsCodeValid && entry.SecondsRemaining <= 5);
    public int RecentlyUsedCount => _allEntries.Count(entry => IsRecentlyUsed(entry.LastUsedAtUtc));
    public bool HasEntries => FilteredEntries.Count > 0;
    public bool HasSelection => SelectedEntry is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanEditSelection => SelectedEntry is not null && !IsBusy;
    public bool CanCopyCode => SelectedEntry?.IsCodeValid == true && !IsBusy;
    public string PageSubtitle => "Import a QR screenshot or paste a secret key to generate local verification codes on this device.";
    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No authenticator codes yet"
        : "No authenticator codes match this search";
    public string EmptySubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? "Add a code by importing a QR screenshot or entering a secret key manually."
        : "Try a different name or reset the search.";
    public string DetailSubtitle => SelectedEntry is null
        ? "Select an authenticator code to view the current value."
        : SelectedEntry.KeyTypeDisplay;
    public string EditorModalTitle => IsEditingExisting ? "Edit Authenticator" : "Add Authenticator";
    public string EditorModalSubtitle => "Import a QR screenshot, paste a copied QR image, or enter the secret manually. Only the name, secret, and key type are required.";
    public string AdvancedOptionsNote => "Some authenticator apps ignore advanced settings. ShellKrypt preserves them locally when supported.";
    public string SaveButtonText => IsEditingExisting ? "Save Changes" : "Add Code";
    public string FormSecretVisibilityText => IsFormSecretVisible ? "Hide" : "Show";
    public string DeleteConfirmationText => SelectedEntry is null
        ? "Delete this authenticator code?"
        : $"Delete {SelectedEntry.Name}?";
    public string SelectedTypeSummary => SelectedFormKeyType?.KeyType == AuthenticatorKeyType.CounterBased
        ? $"Counter starts at {_formCounter}. {SelectedFormAlgorithm?.ShortLabel ?? "SHA1"}, {SelectedFormDigits?.Digits ?? 6} digits."
        : $"Code rotates every {NormalizePeriodText(FormPeriodSecondsText)} seconds. {SelectedFormAlgorithm?.ShortLabel ?? "SHA1"}, {SelectedFormDigits?.Digits ?? 6} digits.";
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
}

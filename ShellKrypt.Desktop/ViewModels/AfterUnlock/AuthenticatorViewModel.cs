using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed class AuthenticatorKeyTypeOption
{
    public AuthenticatorKeyTypeOption(AuthenticatorKeyType keyType, string label)
    {
        KeyType = keyType;
        Label = label;
    }

    public AuthenticatorKeyType KeyType { get; }
    public string Label { get; }
}

public sealed class AuthenticatorAlgorithmOption
{
    public AuthenticatorAlgorithmOption(string value, string label, string shortLabel)
    {
        Value = value;
        Label = label;
        ShortLabel = shortLabel;
    }

    public string Value { get; }
    public string Label { get; }
    public string ShortLabel { get; }
}

public sealed class AuthenticatorDigitsOption
{
    public AuthenticatorDigitsOption(int digits, string label)
    {
        Digits = digits;
        Label = label;
    }

    public int Digits { get; }
    public string Label { get; }
}

public sealed partial class AuthenticatorAccountVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string secret;
    [ObservableProperty] private AuthenticatorKeyType keyType;
    [ObservableProperty] private long counter;
    [ObservableProperty] private string algorithm;
    [ObservableProperty] private int digits;
    [ObservableProperty] private int periodSeconds;
    [ObservableProperty] private string lastUsedAtUtc;
    [ObservableProperty] private string currentCodeRaw;
    [ObservableProperty] private int secondsRemaining;
    [ObservableProperty] private double progressPercent;
    [ObservableProperty] private bool isCodeValid;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isSecretVisible;

    public AuthenticatorAccountVm(AuthenticatorEntry entry)
    {
        Id = entry.Id;
        CreatedAtUtc = entry.CreatedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        Name = entry.Name;
        Secret = entry.Secret;
        KeyType = entry.KeyType;
        Counter = entry.Counter;
        Algorithm = entry.Algorithm;
        Digits = entry.Digits;
        PeriodSeconds = entry.PeriodSeconds;
        LastUsedAtUtc = entry.LastUsedAtUtc;
        CurrentCodeRaw = "------";
        SecondsRemaining = 0;
        ProgressPercent = 0;
        IsCodeValid = false;
    }

    public string Monogram
    {
        get
        {
            var letters = Name
                .Where(char.IsLetterOrDigit)
                .Take(2)
                .ToArray();

            return letters.Length == 0
                ? "AU"
                : new string(letters).ToUpperInvariant();
        }
    }

    public string AccountSubtitle => KeyType == AuthenticatorKeyType.CounterBased
        ? "Counter based code"
        : "Time based code";

    public string CurrentCodeDisplay => FormatCode(CurrentCodeRaw);
    public string RemainingDisplay => SecondsRemaining <= 0 ? "0:00" : $"0:{SecondsRemaining:00}";
    public string SecretDisplay => IsSecretVisible ? FormatSecret(Secret) : "**** **** **** ****";
    public string DigitsDisplay => $"{Digits} digits";
    public string LastUsedDisplay => FormatRelativeTimestamp(LastUsedAtUtc);
    public string VerifiedLabel => IsCodeValid ? "Ready" : "Invalid";
    public string KeyTypeDisplay => KeyType == AuthenticatorKeyType.CounterBased ? "Counter Based" : "Time Based";
    public string AlgorithmDisplay => NormalizeAlgorithmLabel(Algorithm);
    public string RotationDisplay => KeyType == AuthenticatorKeyType.TimeBased
        ? $"{AlgorithmDisplay} · {PeriodSeconds}s"
        : $"{AlgorithmDisplay} · Counter";
    public string CounterDisplay => Counter.ToString(CultureInfo.InvariantCulture);
    public bool HasCountdown => KeyType == AuthenticatorKeyType.TimeBased;
    public string ProgressLabel => KeyType == AuthenticatorKeyType.TimeBased
        ? $"{PeriodSeconds}s rotation"
        : $"Counter {Counter}";
    public string DetailHint => KeyType == AuthenticatorKeyType.TimeBased
        ? "This code rotates automatically."
        : "This code advances when you use it.";
    public string CopyButtonText => KeyType == AuthenticatorKeyType.CounterBased ? "Copy Code & Advance" : "Copy Code";

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Monogram));
    partial void OnCurrentCodeRawChanged(string value) => OnPropertyChanged(nameof(CurrentCodeDisplay));
    partial void OnSecondsRemainingChanged(int value) => OnPropertyChanged(nameof(RemainingDisplay));
    partial void OnSecretChanged(string value) => OnPropertyChanged(nameof(SecretDisplay));
    partial void OnLastUsedAtUtcChanged(string value) => OnPropertyChanged(nameof(LastUsedDisplay));
    partial void OnIsSecretVisibleChanged(bool value) => OnPropertyChanged(nameof(SecretDisplay));
    partial void OnKeyTypeChanged(AuthenticatorKeyType value)
    {
        OnPropertyChanged(nameof(AccountSubtitle));
        OnPropertyChanged(nameof(KeyTypeDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(DetailHint));
        OnPropertyChanged(nameof(CopyButtonText));
    }

    partial void OnCounterChanged(long value)
    {
        OnPropertyChanged(nameof(CounterDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(ProgressLabel));
    }

    partial void OnPeriodSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(ProgressLabel));
    }

    partial void OnAlgorithmChanged(string value)
    {
        OnPropertyChanged(nameof(AlgorithmDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
    }

    public void Apply(AuthenticatorEntry entry)
    {
        Name = entry.Name;
        Secret = entry.Secret;
        KeyType = entry.KeyType;
        Counter = entry.Counter;
        Algorithm = entry.Algorithm;
        Digits = entry.Digits;
        PeriodSeconds = entry.PeriodSeconds;
        LastUsedAtUtc = entry.LastUsedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
    }

    public void ApplySnapshot(AuthenticatorCodeSnapshot snapshot)
    {
        CurrentCodeRaw = snapshot.Code;
        SecondsRemaining = snapshot.SecondsRemaining;
        ProgressPercent = snapshot.ProgressPercent;
        IsCodeValid = snapshot.IsValid;
    }

    public AuthenticatorEntry ToEntry()
        => new(
            Id,
            Name,
            Secret,
            KeyType,
            Counter,
            Algorithm,
            Digits,
            PeriodSeconds,
            LastUsedAtUtc,
            CreatedAtUtc,
            UpdatedAtUtc);

    private static string FormatCode(string? rawCode)
    {
        var value = (rawCode ?? string.Empty).Trim();
        if (value.Length == 6)
            return $"{value[..3]} {value[3..]}";
        if (value.Length == 8)
            return $"{value[..4]} {value[4..]}";

        return value;
    }

    private static string FormatSecret(string? secret)
    {
        var normalized = new string((secret ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (normalized.Length == 0)
            return string.Empty;

        var groups = new List<string>();
        for (var index = 0; index < normalized.Length; index += 4)
            groups.Add(normalized.Substring(index, Math.Min(4, normalized.Length - index)));

        return string.Join(" ", groups);
    }

    private static string FormatRelativeTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Never used";

        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return "Unknown";

        var delta = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
        if (delta.TotalMinutes < 1)
            return "Just now";
        if (delta.TotalHours < 1)
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta.TotalDays < 1)
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta.TotalDays < 7)
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return timestamp.ToLocalTime().ToString("MMM dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeAlgorithmLabel(string? algorithm)
        => (algorithm ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "HMAC-SHA1" => "SHA1",
            "HMAC-SHA256" => "SHA256",
            "HMAC-SHA512" => "SHA512",
            _ => "SHA1"
        };
}

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

    [RelayCommand]
    private void SelectEntry(AuthenticatorAccountVm? entry)
    {
        if (entry is not null)
            SelectedEntry = entry;
    }

    [RelayCommand]
    private void AddNew()
    {
        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsEditingExisting = false;
        ClearEditorForm();
        IsFormSecretVisible = false;
        IsAdvancedOptionsExpanded = false;
        IsEditorModalOpen = true;
    }

    [RelayCommand]
    private void OpenDetails()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsEditorModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsDetailsModalOpen = true;
    }

    [RelayCommand]
    private void CloseDetails()
    {
        Error = string.Empty;
        IsDetailsModalOpen = false;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsDeleteConfirmOpen = false;
        IsEditingExisting = true;
        PopulateEditorForm(SelectedEntry);
        IsFormSecretVisible = false;
        IsAdvancedOptionsExpanded = false;
        IsEditorModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        BeginEdit();
    }

    [RelayCommand]
    private void CancelEditor()
    {
        Error = string.Empty;
        IsEditorModalOpen = false;
        IsFormSecretVisible = false;
    }

    [RelayCommand]
    private void ToggleSecretVisibility(AuthenticatorAccountVm? entry)
    {
        if (entry is not null)
            entry.IsSecretVisible = !entry.IsSecretVisible;
    }

    [RelayCommand]
    private void ToggleFormSecretVisibility()
    {
        IsFormSecretVisible = !IsFormSecretVisible;
    }

    [RelayCommand]
    private async Task ImportQrScreenshotAsync()
    {
        Error = string.Empty;

        var path = await _root.PickOpenFileAsync(
            "Select QR screenshot",
            [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"],
            "Image File");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            ApplyImportedSecret(_qrImportService.ImportFromImage(path));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PasteQrImageAsync()
    {
        Error = string.Empty;

        try
        {
            var bitmap = await _root.TryGetClipboardBitmapAsync();
            if (bitmap is null)
            {
                Error = "Clipboard does not contain an image to scan.";
                return;
            }

            using (bitmap)
            {
                ApplyImportedSecret(_qrImportService.ImportFromBitmap(bitmap));
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        Error = string.Empty;

        if (SelectedEntry is null || !SelectedEntry.IsCodeValid)
        {
            Error = "No valid code is available to copy.";
            return;
        }

        await _root.CopyToClipboardAsync(SelectedEntry.CurrentCodeRaw);

        if (_root.VaultPath is null)
            return;

        var updated = await _authenticatorService.MarkUsedAsync(_root.VaultPath, _root.VaultKey, SelectedEntry.Id);
        SelectedEntry.Apply(updated);
        RefreshSnapshots();
        await _refreshAllItemsAsync(updated.Id);
        _root.LogActivity("authenticator", "Authenticator code copied", $"Copied code for {updated.Name}.", "info");
    }

    [RelayCommand]
    private void BeginDelete()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsEditorModalOpen = false;
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsDelete()
    {
        BeginDelete();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        Error = string.Empty;
        IsDeleteConfirmOpen = false;
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormName))
        {
            Error = "Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormSecret))
        {
            Error = "Secret key is required.";
            return;
        }

        if (SelectedFormKeyType is null)
        {
            Error = "Select a key type.";
            return;
        }

        IsBusy = true;
        try
        {
            var input = new AuthenticatorInput(
                Name: FormName,
                Secret: FormSecret,
                KeyType: SelectedFormKeyType.KeyType,
                Counter: SelectedFormKeyType.KeyType == AuthenticatorKeyType.CounterBased ? _formCounter : 0,
                Algorithm: SelectedFormAlgorithm?.Value ?? "HMAC-SHA1",
                Digits: SelectedFormDigits?.Digits ?? 6,
                PeriodSeconds: ResolveFormPeriodSeconds());

            if (IsEditingExisting)
            {
                if (SelectedEntry is null)
                {
                    Error = "No authenticator code selected.";
                    return;
                }

                var updated = await _authenticatorService.UpdateAsync(
                    _root.VaultPath,
                    _root.VaultKey,
                    SelectedEntry.Id,
                    SelectedEntry.CreatedAtUtc,
                    input);

                SelectedEntry.Apply(updated);
                RefreshSnapshots();
                await _refreshAllItemsAsync(updated.Id);
                _root.LogActivity("authenticator", "Authenticator updated", $"Updated {updated.Name}.", "info");
            }
            else
            {
                var added = await _authenticatorService.AddAsync(_root.VaultPath, _root.VaultKey, input);
                var vm = new AuthenticatorAccountVm(added);
                _allEntries.Insert(0, vm);
                RefreshSnapshots();
                ApplyFilter(selectEntryId: added.Id);
                await _refreshAllItemsAsync(added.Id);
                _root.LogActivity("authenticator", "Authenticator added", $"Added {added.Name}.", "success");
            }

            IsEditorModalOpen = false;
            IsDetailsModalOpen = false;
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

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (SelectedEntry is null)
        {
            Error = "No authenticator code selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = SelectedEntry;
            await _authenticatorService.DeleteAsync(_root.VaultPath, deleted.Id);
            _allEntries.Remove(deleted);
            ApplyFilter();
            await _refreshAllItemsAsync(null);
            _root.LogActivity("authenticator", "Authenticator deleted", $"Deleted {deleted.Name}.", "warning");
            IsDeleteConfirmOpen = false;
            IsDetailsModalOpen = false;
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

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_allEntries.Count == 0)
            await LoadAsync(itemId);

        var entry = _allEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (entry is null)
        {
            await LoadAsync(itemId);
            entry = _allEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            if (entry is null)
                return false;
        }

        SearchText = string.Empty;
        ApplyFilter(itemId);
        return true;
    }

    private async Task LoadAsync(string? selectEntryId = null)
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        IsBusy = true;
        try
        {
            _allEntries.Clear();
            var entries = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
            foreach (var entry in entries)
                _allEntries.Add(new AuthenticatorAccountVm(entry));

            RefreshSnapshots();
            ApplyFilter(selectEntryId);
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

    private void ApplyFilter(string? selectEntryId = null)
    {
        var query = SearchText?.Trim();
        IEnumerable<AuthenticatorAccountVm> filtered = _allEntries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(entry =>
                entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.KeyTypeDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var snapshot = filtered.ToList();
        FilteredEntries.Clear();
        foreach (var entry in snapshot)
            FilteredEntries.Add(entry);

        var targetId = selectEntryId ?? SelectedEntry?.Id;
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            var target = snapshot.FirstOrDefault(entry => string.Equals(entry.Id, targetId, StringComparison.Ordinal));
            if (target is not null)
            {
                SelectedEntry = target;
                NotifyCountProperties();
                return;
            }
        }

        SelectedEntry = snapshot.FirstOrDefault();
        NotifyCountProperties();
    }

    private void RefreshSnapshots()
    {
        foreach (var entry in _allEntries)
            entry.ApplySnapshot(_authenticatorService.GetCurrentCode(entry.ToEntry()));

        OnPropertyChanged(nameof(RefreshingSoonCount));
        OnPropertyChanged(nameof(RecentlyUsedCount));
        OnPropertyChanged(nameof(CanCopyCode));
    }

    private void NotifyCountProperties()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(RefreshingSoonCount));
        OnPropertyChanged(nameof(RecentlyUsedCount));
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
        OnPropertyChanged(nameof(DetailSubtitle));
        OnPropertyChanged(nameof(CanCopyCode));
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    private void PopulateEditorForm(AuthenticatorAccountVm entry)
    {
        FormName = entry.Name;
        FormSecret = entry.Secret;
        SelectedFormKeyType = KeyTypeOptions.First(option => option.KeyType == entry.KeyType);
        _formCounter = entry.Counter;
        SelectedFormAlgorithm = ResolveAlgorithmOption(entry.Algorithm);
        SelectedFormDigits = ResolveDigitsOption(entry.Digits);
        FormPeriodSecondsText = entry.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }

    private void ClearEditorForm()
    {
        FormName = string.Empty;
        FormSecret = string.Empty;
        SelectedFormKeyType = KeyTypeOptions[0];
        SelectedFormAlgorithm = AlgorithmOptions[0];
        SelectedFormDigits = DigitsOptions[0];
        FormPeriodSecondsText = "30";
        _formCounter = 0;
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }

    private void ApplyImportedSecret(ParsedOtpAuthSecret parsed)
    {
        FormName = parsed.Name;
        FormSecret = parsed.Secret;
        SelectedFormKeyType = KeyTypeOptions.First(option => option.KeyType == parsed.KeyType);
        SelectedFormAlgorithm = ResolveAlgorithmOption(parsed.Algorithm);
        SelectedFormDigits = ResolveDigitsOption(parsed.Digits);
        FormPeriodSecondsText = parsed.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        _formCounter = parsed.Counter;
        IsEditorModalOpen = true;
        IsFormSecretVisible = false;
        OnPropertyChanged(nameof(SelectedTypeSummary));
    }

    private AuthenticatorAlgorithmOption ResolveAlgorithmOption(string? value)
    {
        var normalized = NormalizeAlgorithm(value);
        return AlgorithmOptions.First(option => string.Equals(option.Value, normalized, StringComparison.Ordinal));
    }

    private AuthenticatorDigitsOption ResolveDigitsOption(int digits)
    {
        var normalized = digits == 8 ? 8 : 6;
        return DigitsOptions.First(option => option.Digits == normalized);
    }

    private int ResolveFormPeriodSeconds()
    {
        if (SelectedFormKeyType?.KeyType == AuthenticatorKeyType.CounterBased)
            return 30;

        if (!int.TryParse(FormPeriodSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 1 ||
            seconds > 300)
        {
            throw new InvalidOperationException("Period must be a whole number between 1 and 300 seconds.");
        }

        return seconds;
    }

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => "HMAC-SHA1"
        };
    }

    private static string NormalizePeriodText(string? value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : "30";
    }

    private static bool IsRecentlyUsed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DateTimeOffset.TryParse(value, out var timestamp) &&
               timestamp >= DateTimeOffset.UtcNow.Subtract(RecentlyUsedWindow);
    }
}

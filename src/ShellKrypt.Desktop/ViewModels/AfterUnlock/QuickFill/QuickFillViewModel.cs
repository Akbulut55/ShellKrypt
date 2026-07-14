using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.ViewModels.QuickFill;

namespace ShellKrypt.Desktop.ViewModels.AfterUnlock.QuickFill;

public sealed partial class QuickFillViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IQuickFillEntryService _entryService;
    private readonly IWebLoginService _webLoginService;
    private readonly ICardService _cardService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IAuthenticatorEntryService _authenticatorService;

    [ObservableProperty] private QuickFillEntryRowVm? selectedEntry;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private QuickFillFilterOption? selectedCategoryFilter;
    [ObservableProperty] private QuickFillFilterOption? selectedTargetFilter;
    [ObservableProperty] private bool enabledOnly;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;

    public QuickFillViewModel(
        MainWindowViewModel root,
        IQuickFillEntryService entryService,
        IWebLoginService webLoginService,
        ICardService cardService,
        IApiKeyService apiKeyService,
        IAuthenticatorEntryService authenticatorService)
    {
        _root = root;
        _entryService = entryService;
        _webLoginService = webLoginService;
        _cardService = cardService;
        _apiKeyService = apiKeyService;
        _authenticatorService = authenticatorService;

        Editor = new QuickFillEntryEditorVm(T)
        {
            SaveRequested = SaveEditorEntryAsync,
            DeleteRequested = DeleteEditorEntryAsync,
            CancelRequested = CancelEditorEdit
        };

        _ = LoadAsync();
    }

    public QuickFillEntryEditorVm Editor { get; }
    public ObservableCollection<QuickFillEntryRowVm> Entries { get; } = new();
    public ObservableCollection<QuickFillEntryRowVm> FilteredEntries { get; } = new();
    public ObservableCollection<QuickFillFilterOption> CategoryFilters { get; } = new();
    public ObservableCollection<QuickFillFilterOption> TargetFilters { get; } = new();

    public bool HasEntries => Entries.Count > 0;
    public bool HasFilteredEntries => FilteredEntries.Count > 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public int EnabledEntryCount => Entries.Count(entry => entry.Entry.Enabled);
    public int TargetAppCount => Entries
        .Select(entry => NormalizeFilterId(entry.Entry.Target.ProcessName))
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public int LinkedFieldCount => Entries.Sum(entry => entry.Entry.Fields.Count(itemField => itemField.SourceKind != QuickFillFieldSourceKind.Owned));
    public string AutoTypeReadyStatus => _root.QuickFill.HasAutoTypeAcknowledgement
        ? T("QuickFill.State.Ready")
        : T("QuickFill.State.NeedsWarning");
    public string HotkeyStatus => _root.QuickFillHotkeyStatus;
    public bool CanConfigureSystemShortcut => _root.CanConfigureQuickFillSystemShortcut;
    public string AutoTypeAcknowledgementText => _root.QuickFill.HasAutoTypeAcknowledgement
        ? T("QuickFill.AutoType.Acknowledged")
        : T("QuickFill.AutoType.NotAcknowledged");

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnSelectedEntryChanged(QuickFillEntryRowVm? value)
    {
        Editor.CanDeleteEntry = value is not null;
        if (value is not null)
            Editor.Populate(value.Entry);
    }

    partial void OnSearchTextChanged(string value) => ApplyEntryFilters();
    partial void OnSelectedCategoryFilterChanged(QuickFillFilterOption? value) => ApplyEntryFilters();
    partial void OnSelectedTargetFilterChanged(QuickFillFilterOption? value) => ApplyEntryFilters();
    partial void OnEnabledOnlyChanged(bool value) => ApplyEntryFilters();

    public async Task LoadAsync()
    {
        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        IsBusy = true;
        try
        {
            var webLogins = await _webLoginService.ListAsync(_root.VaultPath, _root.VaultKey);
            var creditCards = await _cardService.ListAsync(_root.VaultPath, _root.VaultKey);
            var apiKeys = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
            var authenticators = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
            Editor.SetLinkedSources(webLogins, creditCards, apiKeys, authenticators);

            Entries.Clear();
            var entries = await _entryService.ListAsync(_root.VaultPath, _root.VaultKey);
            foreach (var entry in entries.OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
                Entries.Add(new QuickFillEntryRowVm(entry, _root));

            RefreshEntryFilterOptions();
            ApplyEntryFilters();
            NotifyEntryMetricsChanged();
            if (SelectedEntry is null && FilteredEntries.Count > 0)
                SelectedEntry = FilteredEntries[0];
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void PrepareEntryFromTarget(QuickFillTargetContext target)
    {
        SelectedEntry = null;
        Editor.CanDeleteEntry = false;
        Editor.PrepareFromTarget(target);
        Status = "";
    }

    public void RefreshHotkeyStatus()
    {
        OnPropertyChanged(nameof(HotkeyStatus));
        OnPropertyChanged(nameof(CanConfigureSystemShortcut));
    }

    public override void RefreshLocalization()
    {
        Editor.RefreshLocalization();
        foreach (var entry in Entries)
            entry.RefreshLocalization();

        RefreshEntryFilterOptions();
        NotifyLocalized(nameof(AutoTypeAcknowledgementText));
        NotifyLocalized(nameof(AutoTypeReadyStatus));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ConfigureSystemShortcut() => _root.ConfigureQuickFillSystemShortcut();

    [RelayCommand]
    private void NewEntry()
    {
        SelectedEntry = null;
        Editor.CanDeleteEntry = false;
        Editor.Reset();
        Status = "";
    }

    [RelayCommand]
    private async Task SetEntryEnabledAsync(QuickFillEntryRowVm? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entry = row.Entry;
        var input = ToInput(entry, row.IsEnabled);
        var updated = await _entryService.UpdateAsync(_root.VaultPath, _root.VaultKey, entry.Id, entry.CreatedAtUtc, input);
        ReplaceEntry(updated);
        _root.LogActivity("quick-fill", "Quick Fill entry updated", $"Updated Quick Fill entry {updated.Name}.", "success", _root.VaultPath, updated.Name);
        Status = T("QuickFill.Status.Saved", updated.Name);
    }

    [RelayCommand]
    private async Task DeleteEntryRowAsync(QuickFillEntryRowVm? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entryName = row.Entry.Name;
        var confirmed = await _root.ConfirmAsync(
            T("QuickFill.Delete.Title"),
            T("QuickFill.Delete.Subtitle", entryName),
            T("Common.Delete"),
            destructive: true);
        if (!confirmed)
            return;

        await _entryService.DeleteAsync(_root.VaultPath, row.Entry.Id);
        _root.LogActivity("quick-fill", "Quick Fill entry deleted", $"Deleted Quick Fill entry {entryName}.", "warning", _root.VaultPath, entryName);
        await LoadAsync();
        Status = T("QuickFill.Status.Deleted", entryName);
    }

    [RelayCommand]
    private void AcknowledgeAutoType()
    {
        _root.AcceptQuickFillAutoTypeAcknowledgement();
        OnPropertyChanged(nameof(AutoTypeAcknowledgementText));
        OnPropertyChanged(nameof(AutoTypeReadyStatus));
        Status = T("QuickFill.Status.AutoTypeAcknowledged");
    }

    private async Task SaveEditorEntryAsync(QuickFillEntryInput input)
    {
        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            Status = T("QuickFill.Status.UnlockRequired");
            return;
        }

        try
        {
            QuickFillEntry saved;
            if (SelectedEntry is null)
            {
                saved = await _entryService.AddAsync(_root.VaultPath, _root.VaultKey, input);
                _root.LogActivity("quick-fill", "Quick Fill entry created", $"Created Quick Fill entry {saved.Name}.", "success", _root.VaultPath, saved.Name);
            }
            else
            {
                saved = await _entryService.UpdateAsync(_root.VaultPath, _root.VaultKey, SelectedEntry.Entry.Id, SelectedEntry.Entry.CreatedAtUtc, input);
                _root.LogActivity("quick-fill", "Quick Fill entry updated", $"Updated Quick Fill entry {saved.Name}.", "success", _root.VaultPath, saved.Name);
            }

            await LoadAsync();
            SelectedEntry = Entries.FirstOrDefault(entry => entry.Entry.Id == saved.Id);
            Status = T("QuickFill.Status.Saved", saved.Name);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async Task DeleteEditorEntryAsync()
    {
        if (SelectedEntry is null || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entryName = SelectedEntry.Entry.Name;
        var confirmed = await _root.ConfirmAsync(
            T("QuickFill.Delete.Title"),
            T("QuickFill.Delete.Subtitle", entryName),
            T("Common.Delete"),
            destructive: true);
        if (!confirmed)
            return;

        await _entryService.DeleteAsync(_root.VaultPath, SelectedEntry.Entry.Id);
        _root.LogActivity("quick-fill", "Quick Fill entry deleted", $"Deleted Quick Fill entry {entryName}.", "warning", _root.VaultPath, entryName);
        SelectedEntry = null;
        await LoadAsync();
        NewEntry();
        Status = T("QuickFill.Status.Deleted", entryName);
    }

    private void CancelEditorEdit()
    {
        if (SelectedEntry is null)
            NewEntry();
        else
            Editor.Populate(SelectedEntry.Entry);
    }

    private void RefreshEntryFilterOptions()
    {
        var selectedCategoryId = SelectedCategoryFilter?.Id ?? "";
        var selectedTargetId = SelectedTargetFilter?.Id ?? "";

        CategoryFilters.Clear();
        CategoryFilters.Add(new QuickFillFilterOption("", T("QuickFill.Filter.AllCategories")));
        foreach (var category in Entries
                     .Select(entry => entry.Entry.Category)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            CategoryFilters.Add(new QuickFillFilterOption(NormalizeFilterId(category), category));
        }

        TargetFilters.Clear();
        TargetFilters.Add(new QuickFillFilterOption("", T("QuickFill.Filter.AllTargets")));
        foreach (var target in Entries
                     .Select(entry => entry.Entry.Target.ProcessName)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            TargetFilters.Add(new QuickFillFilterOption(NormalizeFilterId(target), target));
        }

        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(option => string.Equals(option.Id, selectedCategoryId, StringComparison.OrdinalIgnoreCase))
            ?? CategoryFilters.FirstOrDefault();
        SelectedTargetFilter = TargetFilters.FirstOrDefault(option => string.Equals(option.Id, selectedTargetId, StringComparison.OrdinalIgnoreCase))
            ?? TargetFilters.FirstOrDefault();
    }

    private void ApplyEntryFilters()
    {
        FilteredEntries.Clear();
        IEnumerable<QuickFillEntryRowVm> filtered = Entries;

        var search = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(entry => EntryMatchesSearch(entry, search));

        var categoryId = SelectedCategoryFilter?.Id ?? "";
        if (!string.IsNullOrWhiteSpace(categoryId))
            filtered = filtered.Where(entry => string.Equals(NormalizeFilterId(entry.Entry.Category), categoryId, StringComparison.OrdinalIgnoreCase));

        var targetId = SelectedTargetFilter?.Id ?? "";
        if (!string.IsNullOrWhiteSpace(targetId))
            filtered = filtered.Where(entry => string.Equals(NormalizeFilterId(entry.Entry.Target.ProcessName), targetId, StringComparison.OrdinalIgnoreCase));

        if (EnabledOnly)
            filtered = filtered.Where(entry => entry.Entry.Enabled);

        foreach (var entry in filtered)
            FilteredEntries.Add(entry);

        OnPropertyChanged(nameof(HasFilteredEntries));
    }

    private static bool EntryMatchesSearch(QuickFillEntryRowVm entry, string search)
    {
        var haystack = string.Join(' ', [
            entry.Entry.Name,
            entry.Entry.Category,
            entry.Entry.Target.ProcessName,
            entry.Entry.Target.WindowTitleContains,
            .. entry.Entry.Fields.Select(field => field.Label),
            .. entry.Entry.Fields.Select(field => field.SourceKind.ToString())
        ]);
        return haystack.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyEntryMetricsChanged()
    {
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EnabledEntryCount));
        OnPropertyChanged(nameof(TargetAppCount));
        OnPropertyChanged(nameof(LinkedFieldCount));
    }

    private static string NormalizeFilterId(string? value)
        => (value ?? "").Trim().ToUpperInvariant();

    private static QuickFillEntryInput ToInput(QuickFillEntry entry, bool enabled)
        => new(
            entry.Name,
            entry.Category,
            enabled,
            entry.Target,
            entry.Fields,
            entry.PressEnterAfterFill,
            entry.Notes,
            entry.SequenceSteps);

    private void ReplaceEntry(QuickFillEntry updated)
    {
        var existing = Entries.FirstOrDefault(entry => entry.Entry.Id == updated.Id);
        if (existing is not null)
            Entries[Entries.IndexOf(existing)] = new QuickFillEntryRowVm(updated, _root);

        RefreshEntryFilterOptions();
        ApplyEntryFilters();
        NotifyEntryMetricsChanged();
        SelectedEntry = Entries.FirstOrDefault(entry => entry.Entry.Id == updated.Id) ?? SelectedEntry;
    }

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);
}

public sealed partial class QuickFillEntryRowVm : ObservableObject
{
    private readonly MainWindowViewModel _root;

    public QuickFillEntryRowVm(QuickFillEntry entry, MainWindowViewModel root)
    {
        Entry = entry;
        _root = root;
        isEnabled = entry.Enabled;
    }

    public QuickFillEntry Entry { get; }
    [ObservableProperty] private bool isEnabled;
    public string Name => Entry.Name;
    public string Category => Entry.Category;
    public string TargetDisplay => string.IsNullOrWhiteSpace(Entry.Target.WindowTitleContains)
        ? Entry.Target.ProcessName
        : $"{Entry.Target.ProcessName} / {Entry.Target.WindowTitleContains}";
    public string FieldSummary => Entry.Fields.Count == 1
        ? _root.Localization.Get("QuickFill.Entry.OneField")
        : _root.Localization.Get("QuickFill.Entry.FieldCount", Entry.Fields.Count);
    public string EnabledDisplay => Entry.Enabled
        ? _root.Localization.Get("QuickFill.State.Enabled")
        : _root.Localization.Get("QuickFill.State.Disabled");

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(FieldSummary));
        OnPropertyChanged(nameof(EnabledDisplay));
    }
}

public sealed class QuickFillFilterOption
{
    public QuickFillFilterOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }

    public override string ToString() => Label;
}

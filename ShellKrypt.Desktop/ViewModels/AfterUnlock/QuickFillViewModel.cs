using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.QuickFill;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class QuickFillViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IQuickFillEntryService _entryService;
    private readonly IWebLoginService _webLoginService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IAuthenticatorService _authenticatorService;

    private IReadOnlyList<WebLoginEntry> _webLogins = [];
    private IReadOnlyList<ApiKeyEntry> _apiKeys = [];
    private IReadOnlyList<AuthenticatorEntry> _authenticators = [];

    [ObservableProperty] private QuickFillEntryRowVm? selectedEntry;
    [ObservableProperty] private string entryName = "";
    [ObservableProperty] private string entryCategory = "Other";
    [ObservableProperty] private bool entryEnabled = true;
    [ObservableProperty] private string targetProcessName = "";
    [ObservableProperty] private string targetWindowTitleContains = "";
    [ObservableProperty] private bool pressEnterAfterFill;
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private string ownedFieldLabel = "";
    [ObservableProperty] private string ownedFieldValue = "";
    [ObservableProperty] private QuickFillFieldKindOption? selectedOwnedFieldKind;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedWebLoginOption;
    [ObservableProperty] private QuickFillWebLoginFieldOption? selectedWebLoginFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedAuthenticatorOption;
    [ObservableProperty] private QuickFillSequenceStepEditorVm? selectedSequenceStep;
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
        IApiKeyService apiKeyService,
        IAuthenticatorService authenticatorService)
    {
        _root = root;
        _entryService = entryService;
        _webLoginService = webLoginService;
        _apiKeyService = apiKeyService;
        _authenticatorService = authenticatorService;

        foreach (var option in CreateFieldKindOptions())
            FieldKindOptions.Add(option);

        foreach (var option in CreateWebLoginFieldOptions())
            WebLoginFieldOptions.Add(option);
        foreach (var option in CreateSequenceStepKindOptions())
            SequenceStepKindOptions.Add(option);
        foreach (var option in CreateKeystrokeOptions())
            KeystrokeOptions.Add(option);

        SelectedOwnedFieldKind = FieldKindOptions.FirstOrDefault();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
        _ = LoadAsync();
    }

    public ObservableCollection<QuickFillEntryRowVm> Entries { get; } = new();
    public ObservableCollection<QuickFillEntryRowVm> FilteredEntries { get; } = new();
    public ObservableCollection<QuickFillFieldEditorVm> Fields { get; } = new();
    public ObservableCollection<QuickFillFieldKindOption> FieldKindOptions { get; } = new();
    public ObservableCollection<QuickFillWebLoginFieldOption> WebLoginFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> WebLoginOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> AuthenticatorOptions { get; } = new();
    public ObservableCollection<QuickFillFilterOption> CategoryFilters { get; } = new();
    public ObservableCollection<QuickFillFilterOption> TargetFilters { get; } = new();
    public ObservableCollection<QuickFillSequenceStepEditorVm> SequenceSteps { get; } = new();
    public ObservableCollection<QuickFillSequenceStepKindOption> SequenceStepKindOptions { get; } = new();
    public ObservableCollection<QuickFillKeystrokeOption> KeystrokeOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceFieldOption> SequenceFieldOptions { get; } = new();

    public bool HasEntries => Entries.Count > 0;
    public bool HasFilteredEntries => FilteredEntries.Count > 0;
    public bool HasFields => Fields.Count > 0;
    public bool HasSequenceSteps => SequenceSteps.Count > 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasTargetWindowTitleContains => !string.IsNullOrWhiteSpace(TargetWindowTitleContains);
    public string TargetProcessDisplay => string.IsNullOrWhiteSpace(TargetProcessName)
        ? T("QuickFill.Editor.TargetNotSet")
        : TargetProcessName;
    public string SequenceStepCountText => T("QuickFill.Sequence.ConfiguredCount", SequenceSteps.Count);
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
    public string AutoTypeAcknowledgementText => _root.QuickFill.HasAutoTypeAcknowledgement
        ? T("QuickFill.AutoType.Acknowledged")
        : T("QuickFill.AutoType.NotAcknowledged");

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnSelectedEntryChanged(QuickFillEntryRowVm? value) => PopulateEditor(value?.Entry);
    partial void OnSelectedApiKeyOptionChanged(QuickFillLinkedItemOption? value) => RefreshApiKeyFieldOptions(value?.Id);
    partial void OnTargetProcessNameChanged(string value) => OnPropertyChanged(nameof(TargetProcessDisplay));
    partial void OnTargetWindowTitleContainsChanged(string value) => OnPropertyChanged(nameof(HasTargetWindowTitleContains));
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
            _webLogins = await _webLoginService.ListAsync(_root.VaultPath, _root.VaultKey);
            _apiKeys = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
            _authenticators = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
            RefreshLinkedOptions();

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
        NewEntry();
        TargetProcessName = target.ProcessName;
        TargetWindowTitleContains = target.WindowTitle;
        EntryName = string.IsNullOrWhiteSpace(target.ProcessName)
            ? T("QuickFill.Editor.NewEntry")
            : target.ProcessName;
        Status = "";
    }

    public void RefreshHotkeyStatus()
    {
        OnPropertyChanged(nameof(HotkeyStatus));
    }

    public override void RefreshLocalization()
    {
        foreach (var option in FieldKindOptions)
            option.RefreshLocalization(_root);
        foreach (var option in WebLoginFieldOptions)
            option.RefreshLocalization(_root);
        foreach (var option in SequenceStepKindOptions)
            option.RefreshLocalization(_root);
        foreach (var option in KeystrokeOptions)
            option.RefreshLocalization(_root);
        foreach (var entry in Entries)
            entry.RefreshLocalization();

        RefreshEntryFilterOptions();
        NotifyLocalized(nameof(AutoTypeAcknowledgementText));
        NotifyLocalized(nameof(AutoTypeReadyStatus));
        NotifyLocalized(nameof(SequenceStepCountText));
        NotifyLocalized(nameof(TargetProcessDisplay));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void NewEntry()
    {
        SelectedEntry = null;
        EntryName = T("QuickFill.Editor.NewEntry");
        EntryCategory = "Other";
        EntryEnabled = true;
        TargetProcessName = "";
        TargetWindowTitleContains = "";
        PressEnterAfterFill = false;
        Notes = "";
        Fields.Clear();
        SequenceSteps.Clear();
        RefreshSequenceFieldOptions();
        ResetAddFieldInputs();
        Status = "";
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasSequenceSteps));
    }

    [RelayCommand]
    private void AddOwnedField()
    {
        var kind = SelectedOwnedFieldKind?.Kind ?? QuickFillFieldKind.Text;
        var label = string.IsNullOrWhiteSpace(OwnedFieldLabel)
            ? DefaultFieldLabel(kind)
            : OwnedFieldLabel.Trim();
        AppendFieldAndStep(QuickFillFieldEditorVm.FromField(new QuickFillField(
            Guid.NewGuid().ToString("N"),
            label,
            kind,
            IsSensitiveKind(kind),
            Fields.Count,
            QuickFillFieldSourceKind.Owned,
            OwnedFieldValue,
            "",
            "",
            "")));
        OwnedFieldLabel = "";
        OwnedFieldValue = "";
    }

    [RelayCommand]
    private void AddWebLoginField()
    {
        if (SelectedWebLoginOption is null || SelectedWebLoginFieldOption is null)
            return;

        AppendFieldAndStep(QuickFillFieldEditorVm.FromField(new QuickFillField(
            Guid.NewGuid().ToString("N"),
            SelectedWebLoginFieldOption.Label,
            SelectedWebLoginFieldOption.Kind,
            SelectedWebLoginFieldOption.IsSensitive,
            Fields.Count,
            QuickFillFieldSourceKind.WebLogin,
            "",
            SelectedWebLoginOption.Id,
            "",
            SelectedWebLoginFieldOption.FieldName)));
    }

    [RelayCommand]
    private void AddApiKeyField()
    {
        if (SelectedApiKeyOption is null || SelectedApiKeyFieldOption is null)
            return;

        AppendFieldAndStep(QuickFillFieldEditorVm.FromField(new QuickFillField(
            Guid.NewGuid().ToString("N"),
            SelectedApiKeyFieldOption.Label,
            QuickFillFieldKind.Secret,
            true,
            Fields.Count,
            QuickFillFieldSourceKind.ApiKeyField,
            "",
            SelectedApiKeyOption.Id,
            SelectedApiKeyFieldOption.Id,
            "")));
    }

    [RelayCommand]
    private void AddAuthenticatorField()
    {
        if (SelectedAuthenticatorOption is null)
            return;

        AppendFieldAndStep(QuickFillFieldEditorVm.FromField(new QuickFillField(
            Guid.NewGuid().ToString("N"),
            T("QuickFill.Field.Otp"),
            QuickFillFieldKind.Otp,
            true,
            Fields.Count,
            QuickFillFieldSourceKind.Authenticator,
            "",
            SelectedAuthenticatorOption.Id,
            "",
            "totp")));
    }

    [RelayCommand]
    private void RemoveField(QuickFillFieldEditorVm? field)
    {
        if (field is null)
            return;

        Fields.Remove(field);
        foreach (var step in SequenceSteps.Where(step => string.Equals(step.FieldId, field.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
            SequenceSteps.Remove(step);
        ResequenceFields();
        ResequenceSequenceSteps();
        RefreshSequenceFieldOptions();
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasSequenceSteps));
    }

    [RelayCommand]
    private void AddSequenceStep()
    {
        if (SequenceFieldOptions.Count > 0)
        {
            var field = SequenceFieldOptions.First();
            AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Field, SequenceSteps.Count, field.Id, QuickFillKeystrokeKind.Tab, "", 0));
            return;
        }

        AddTabSequenceStep();
    }

    [RelayCommand]
    private void AddFieldSequenceStep()
    {
        if (SequenceFieldOptions.Count == 0)
            return;

        var selected = SelectedSequenceStep?.SelectedFieldOption ?? SequenceFieldOptions.First();
        AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Field, SequenceSteps.Count, selected.Id, QuickFillKeystrokeKind.Tab, "", 0));
    }

    [RelayCommand]
    private void AddTabSequenceStep()
        => AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Keystroke, SequenceSteps.Count, "", QuickFillKeystrokeKind.Tab, "", 0));

    [RelayCommand]
    private void AddEnterSequenceStep()
        => AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Keystroke, SequenceSteps.Count, "", QuickFillKeystrokeKind.Enter, "", 0));

    [RelayCommand]
    private void AddTextSequenceStep()
        => AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.LiteralText, SequenceSteps.Count, "", QuickFillKeystrokeKind.Tab, "", 0));

    [RelayCommand]
    private void AddDelaySequenceStep()
        => AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Delay, SequenceSteps.Count, "", QuickFillKeystrokeKind.Tab, "", 250));

    [RelayCommand]
    private void MoveSequenceStepUp(QuickFillSequenceStepEditorVm? step)
    {
        if (step is null)
            return;

        var index = SequenceSteps.IndexOf(step);
        if (index <= 0)
            return;

        SequenceSteps.Move(index, index - 1);
        ResequenceSequenceSteps();
    }

    [RelayCommand]
    private void MoveSequenceStepDown(QuickFillSequenceStepEditorVm? step)
    {
        if (step is null)
            return;

        var index = SequenceSteps.IndexOf(step);
        if (index < 0 || index >= SequenceSteps.Count - 1)
            return;

        SequenceSteps.Move(index, index + 1);
        ResequenceSequenceSteps();
    }

    [RelayCommand]
    private void RemoveSequenceStep(QuickFillSequenceStepEditorVm? step)
    {
        if (step is null)
            return;

        SequenceSteps.Remove(step);
        ResequenceSequenceSteps();
        OnPropertyChanged(nameof(HasSequenceSteps));
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (SelectedEntry is null)
            NewEntry();
        else
            PopulateEditor(SelectedEntry.Entry);
    }

    [RelayCommand]
    private async Task SaveEntryAsync()
    {
        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            Status = T("QuickFill.Status.UnlockRequired");
            return;
        }

        try
        {
            var input = BuildInput();
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

    [RelayCommand]
    private async Task DeleteEntryAsync()
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

    [RelayCommand]
    private void AcknowledgeAutoType()
    {
        _root.AcceptQuickFillAutoTypeAcknowledgement();
        OnPropertyChanged(nameof(AutoTypeAcknowledgementText));
        OnPropertyChanged(nameof(AutoTypeReadyStatus));
        Status = T("QuickFill.Status.AutoTypeAcknowledged");
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

    private QuickFillEntryInput BuildInput()
    {
        ResequenceFields();
        ResequenceSequenceSteps();
        return new QuickFillEntryInput(
            EntryName,
            EntryCategory,
            EntryEnabled,
            new QuickFillTargetRule(TargetProcessName, TargetWindowTitleContains),
            Fields.Select(field => field.ToField()).ToArray(),
            PressEnterAfterFill,
            Notes,
            SequenceSteps.Select(step => step.ToStep()).ToArray());
    }

    private void PopulateEditor(QuickFillEntry? entry)
    {
        if (entry is null)
            return;

        EntryName = entry.Name;
        EntryCategory = entry.Category;
        EntryEnabled = entry.Enabled;
        TargetProcessName = entry.Target.ProcessName;
        TargetWindowTitleContains = entry.Target.WindowTitleContains;
        PressEnterAfterFill = entry.PressEnterAfterFill;
        Notes = entry.Notes;
        Fields.Clear();
        foreach (var field in entry.Fields.OrderBy(field => field.SortOrder))
            Fields.Add(QuickFillFieldEditorVm.FromField(field));
        RefreshSequenceFieldOptions();
        SequenceSteps.Clear();
        foreach (var step in QuickFillSequencePreviewer.NormalizeSequenceSteps(entry))
            AddSequenceStep(step, notify: false);
        ResetAddFieldInputs();
        Status = "";
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasSequenceSteps));
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    private void RefreshLinkedOptions()
    {
        WebLoginOptions.Clear();
        foreach (var login in _webLogins.OrderBy(login => login.Title, StringComparer.OrdinalIgnoreCase))
            WebLoginOptions.Add(new QuickFillLinkedItemOption(login.Id, login.Title));

        ApiKeyOptions.Clear();
        foreach (var apiKey in _apiKeys.OrderBy(apiKey => apiKey.Name, StringComparer.OrdinalIgnoreCase))
            ApiKeyOptions.Add(new QuickFillLinkedItemOption(apiKey.Id, apiKey.Name));

        AuthenticatorOptions.Clear();
        foreach (var authenticator in _authenticators.OrderBy(authenticator => authenticator.Name, StringComparer.OrdinalIgnoreCase))
            AuthenticatorOptions.Add(new QuickFillLinkedItemOption(authenticator.Id, authenticator.Name));

        SelectedWebLoginOption ??= WebLoginOptions.FirstOrDefault();
        SelectedWebLoginFieldOption ??= WebLoginFieldOptions.FirstOrDefault();
        SelectedApiKeyOption ??= ApiKeyOptions.FirstOrDefault();
        SelectedAuthenticatorOption ??= AuthenticatorOptions.FirstOrDefault();
        RefreshApiKeyFieldOptions(SelectedApiKeyOption?.Id);
    }

    private void RefreshApiKeyFieldOptions(string? apiKeyId)
    {
        ApiKeyFieldOptions.Clear();
        var apiKey = _apiKeys.FirstOrDefault(entry => entry.Id == apiKeyId);
        if (apiKey is not null)
        {
            foreach (var field in apiKey.Fields.OrderBy(field => field.SortOrder))
                ApiKeyFieldOptions.Add(new QuickFillLinkedItemOption(field.Id, $"{apiKey.Name} - {field.Label}"));
        }

        SelectedApiKeyFieldOption = ApiKeyFieldOptions.FirstOrDefault();
    }

    private void ResetAddFieldInputs()
    {
        OwnedFieldLabel = "";
        OwnedFieldValue = "";
        SelectedOwnedFieldKind = FieldKindOptions.FirstOrDefault();
        SelectedWebLoginOption = WebLoginOptions.FirstOrDefault();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
        SelectedApiKeyOption = ApiKeyOptions.FirstOrDefault();
        SelectedApiKeyFieldOption = ApiKeyFieldOptions.FirstOrDefault();
        SelectedAuthenticatorOption = AuthenticatorOptions.FirstOrDefault();
    }

    private void AppendFieldAndStep(QuickFillFieldEditorVm field)
    {
        Fields.Add(field);
        RefreshSequenceFieldOptions();
        EnsureSequenceStepForField(field.Id);
        OnPropertyChanged(nameof(HasFields));
    }

    private void EnsureSequenceStepForField(string fieldId)
    {
        if (SequenceSteps.Any(step => step.Kind == QuickFillSequenceStepKind.Field && string.Equals(step.FieldId, fieldId, StringComparison.OrdinalIgnoreCase)))
            return;

        AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Field, SequenceSteps.Count, fieldId, QuickFillKeystrokeKind.Tab, "", 0));
    }

    private void AddSequenceStep(QuickFillSequenceStep step, bool notify = true)
    {
        SequenceSteps.Add(QuickFillSequenceStepEditorVm.FromStep(step, SequenceStepKindOptions, KeystrokeOptions, SequenceFieldOptions));
        ResequenceSequenceSteps();
        if (notify)
            OnPropertyChanged(nameof(HasSequenceSteps));
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    private void RefreshSequenceFieldOptions()
    {
        SequenceFieldOptions.Clear();
        foreach (var field in Fields.OrderBy(field => field.SortOrder))
            SequenceFieldOptions.Add(new QuickFillSequenceFieldOption(field.Id, SequenceFieldLabel(field), SequenceSourceKey(field), SequenceSourceLabel(field), field.IsSensitive));

        foreach (var step in SequenceSteps)
            step.ReplaceFieldOptions(SequenceFieldOptions);
    }

    private void ResequenceFields()
    {
        var order = 0;
        foreach (var field in Fields)
            field.SortOrder = order++;
    }

    private void ResequenceSequenceSteps()
    {
        var order = 0;
        foreach (var step in SequenceSteps)
            step.SortOrder = order++;
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    private string SequenceSourceKey(QuickFillFieldEditorVm field)
        => field.SourceKind switch
        {
            QuickFillFieldSourceKind.WebLogin => $"web:{field.LinkedItemId}",
            QuickFillFieldSourceKind.ApiKeyField => $"api:{field.LinkedItemId}",
            QuickFillFieldSourceKind.Authenticator => $"auth:{field.LinkedItemId}",
            _ => "manual"
        };

    private string SequenceSourceLabel(QuickFillFieldEditorVm field)
        => field.SourceKind switch
        {
            QuickFillFieldSourceKind.WebLogin => $"{T("QuickFill.Source.WebLogin")} - {WebLoginOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            QuickFillFieldSourceKind.ApiKeyField => $"{T("QuickFill.Source.ApiKey")} - {ApiKeyOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            QuickFillFieldSourceKind.Authenticator => $"{T("QuickFill.Source.Authenticator")} - {AuthenticatorOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            _ => T("QuickFill.Source.Manual")
        };

    private static string SequenceFieldLabel(QuickFillFieldEditorVm field)
        => field.IsSensitive ? $"*** {field.Label}" : field.Label;

    private string DefaultFieldLabel(QuickFillFieldKind kind)
        => kind switch
        {
            QuickFillFieldKind.Username => T("QuickFill.Field.Username"),
            QuickFillFieldKind.Password => T("QuickFill.Field.Password"),
            QuickFillFieldKind.Otp => T("QuickFill.Field.Otp"),
            QuickFillFieldKind.Secret => T("QuickFill.Field.Secret"),
            _ => T("QuickFill.Field.Text")
        };

    private static bool IsSensitiveKind(QuickFillFieldKind kind)
        => kind is QuickFillFieldKind.Password or QuickFillFieldKind.Secret or QuickFillFieldKind.Otp;

    private static string SafeTargetName(QuickFillTargetContext target)
        => string.IsNullOrWhiteSpace(target.ProcessName) ? "target" : target.ProcessName;

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private static QuickFillFieldKindOption[] CreateFieldKindOptions() =>
    [
        new(QuickFillFieldKind.Username, "QuickFill.Field.Username"),
        new(QuickFillFieldKind.Password, "QuickFill.Field.Password"),
        new(QuickFillFieldKind.Text, "QuickFill.Field.Text"),
        new(QuickFillFieldKind.Secret, "QuickFill.Field.Secret"),
        new(QuickFillFieldKind.Otp, "QuickFill.Field.Otp")
    ];

    private static QuickFillWebLoginFieldOption[] CreateWebLoginFieldOptions() =>
    [
        new("username", QuickFillFieldKind.Username, false, "QuickFill.Field.Username"),
        new("email", QuickFillFieldKind.Username, false, "QuickFill.Field.Email"),
        new("password", QuickFillFieldKind.Password, true, "QuickFill.Field.Password"),
        new("url", QuickFillFieldKind.Text, false, "QuickFill.Field.Url")
    ];

    private static QuickFillSequenceStepKindOption[] CreateSequenceStepKindOptions() =>
    [
        new(QuickFillSequenceStepKind.Field, "QuickFill.Sequence.Kind.Field"),
        new(QuickFillSequenceStepKind.Keystroke, "QuickFill.Sequence.Kind.Keystroke"),
        new(QuickFillSequenceStepKind.LiteralText, "QuickFill.Sequence.Kind.LiteralText"),
        new(QuickFillSequenceStepKind.Delay, "QuickFill.Sequence.Kind.Delay")
    ];

    private static QuickFillKeystrokeOption[] CreateKeystrokeOptions() =>
    [
        new(QuickFillKeystrokeKind.Tab, "QuickFill.Sequence.Keystroke.Tab"),
        new(QuickFillKeystrokeKind.Enter, "QuickFill.Sequence.Keystroke.Enter")
    ];
}

public sealed partial class QuickFillEntryRowVm : ObservableObject
{
    private readonly MainWindowViewModel _root;

    public QuickFillEntryRowVm(QuickFillEntry entry, MainWindowViewModel root)
    {
        Entry = entry;
        _root = root;
    }

    public QuickFillEntry Entry { get; }
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

public sealed partial class QuickFillFieldEditorVm : ObservableObject
{
    [ObservableProperty] private string id = "";
    [ObservableProperty] private string label = "";
    [ObservableProperty] private QuickFillFieldKind kind;
    [ObservableProperty] private bool isSensitive;
    [ObservableProperty] private int sortOrder;
    [ObservableProperty] private QuickFillFieldSourceKind sourceKind;
    [ObservableProperty] private string value = "";
    [ObservableProperty] private string linkedItemId = "";
    [ObservableProperty] private string linkedFieldId = "";
    [ObservableProperty] private string linkedFieldName = "";

    public string SourceDisplay => SourceKind switch
    {
        QuickFillFieldSourceKind.WebLogin => "Web Login",
        QuickFillFieldSourceKind.ApiKeyField => "API Key",
        QuickFillFieldSourceKind.Authenticator => "Authenticator",
        _ => "Manual"
    };

    public static QuickFillFieldEditorVm FromField(QuickFillField field)
        => new()
        {
            Id = field.Id,
            Label = field.Label,
            Kind = field.Kind,
            IsSensitive = field.IsSensitive,
            SortOrder = field.SortOrder,
            SourceKind = field.SourceKind,
            Value = field.Value,
            LinkedItemId = field.LinkedItemId,
            LinkedFieldId = field.LinkedFieldId,
            LinkedFieldName = field.LinkedFieldName
        };

    public QuickFillField ToField()
        => new(Id, Label, Kind, IsSensitive, SortOrder, SourceKind, Value, LinkedItemId, LinkedFieldId, LinkedFieldName);
}

public sealed partial class QuickFillFieldKindOption : ObservableObject
{
    private readonly string _labelKey;

    public QuickFillFieldKindOption(QuickFillFieldKind kind, string labelKey)
    {
        Kind = kind;
        _labelKey = labelKey;
        Label = labelKey;
    }

    public QuickFillFieldKind Kind { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(MainWindowViewModel root) => Label = root.Localization.Get(_labelKey);
    public override string ToString() => Label;
}

public sealed partial class QuickFillWebLoginFieldOption : ObservableObject
{
    private readonly string _labelKey;

    public QuickFillWebLoginFieldOption(string fieldName, QuickFillFieldKind kind, bool isSensitive, string labelKey)
    {
        FieldName = fieldName;
        Kind = kind;
        IsSensitive = isSensitive;
        _labelKey = labelKey;
        Label = labelKey;
    }

    public string FieldName { get; }
    public QuickFillFieldKind Kind { get; }
    public bool IsSensitive { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(MainWindowViewModel root) => Label = root.Localization.Get(_labelKey);
    public override string ToString() => Label;
}

public sealed class QuickFillLinkedItemOption
{
    public QuickFillLinkedItemOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }

    public override string ToString() => Label;
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.QuickFill;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class QuickFillPopupViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly VaultRegistryService _vaultRegistryService;
    private readonly IQuickFillEntryService _entryService;
    private readonly IWebLoginService _webLoginService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IAuthenticatorService _authenticatorService;
    private readonly AutoTypeService _autoTypeService;
    private readonly QuickFillTargetContext _target;

    private QuickFillPopupEntryVm? _editingEntry;
    private IReadOnlyList<WebLoginEntry> _webLogins = Array.Empty<WebLoginEntry>();
    private IReadOnlyList<ApiKeyEntry> _apiKeys = Array.Empty<ApiKeyEntry>();
    private IReadOnlyList<AuthenticatorEntry> _authenticators = Array.Empty<AuthenticatorEntry>();

    [ObservableProperty] private bool isLocked;
    [ObservableProperty] private bool isEditingEntry;
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private VaultChoiceVm? selectedVault;
    [ObservableProperty] private QuickFillPopupEntryVm? selectedEntry;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string entryName = "";
    [ObservableProperty] private string entryCategory = "Other";
    [ObservableProperty] private bool entryEnabled = true;
    [ObservableProperty] private string targetProcessName = "";
    [ObservableProperty] private string targetWindowTitleContains = "";
    [ObservableProperty] private bool pressEnterAfterFill;
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private string ownedFieldLabel = "";
    [ObservableProperty] private string ownedFieldValue = "";
    [ObservableProperty] private bool showAllForApp;
    [ObservableProperty] private QuickFillFieldKindOption? selectedOwnedFieldKind;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedWebLoginOption;
    [ObservableProperty] private QuickFillWebLoginFieldOption? selectedWebLoginFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedAuthenticatorOption;

    public QuickFillPopupViewModel(
        MainWindowViewModel root,
        VaultRegistryService vaultRegistryService,
        IVaultService vaultService,
        IQuickFillEntryService entryService,
        IWebLoginService webLoginService,
        IApiKeyService apiKeyService,
        IAuthenticatorService authenticatorService,
        AutoTypeService autoTypeService,
        QuickFillTargetContext target)
    {
        _root = root;
        _vaultRegistryService = vaultRegistryService;
        _entryService = entryService;
        _webLoginService = webLoginService;
        _apiKeyService = apiKeyService;
        _authenticatorService = authenticatorService;
        _autoTypeService = autoTypeService;
        _target = target;

        foreach (var option in CreateFieldKindOptions())
            FieldKindOptions.Add(option);
        foreach (var option in CreateWebLoginFieldOptions())
            WebLoginFieldOptions.Add(option);
        RefreshOptionLocalization();
        SelectedOwnedFieldKind = FieldKindOptions.FirstOrDefault();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
    }

    public event EventHandler? CloseRequested;

    public ObservableCollection<VaultChoiceVm> VaultChoices { get; } = new();
    public ObservableCollection<QuickFillPopupEntryVm> Matches { get; } = new();
    public ObservableCollection<QuickFillFieldEditorVm> Fields { get; } = new();
    public ObservableCollection<QuickFillFieldKindOption> FieldKindOptions { get; } = new();
    public ObservableCollection<QuickFillWebLoginFieldOption> WebLoginFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> WebLoginOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> AuthenticatorOptions { get; } = new();

    public bool IsUnlocked => !IsLocked;
    public bool IsBrowsingEntries => IsUnlocked && !IsEditingEntry;
    public bool HasMatches => IsBrowsingEntries && Matches.Count > 0;
    public bool HasNoMatches => IsBrowsingEntries && Matches.Count == 0;
    public bool HasFields => Fields.Count > 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string MatchScopeLabel => ShowAllForApp
        ? T("QuickFill.Popup.Scope.Window")
        : T("QuickFill.Popup.Scope.App", SafeTargetName(_target));
    public string TargetDisplay => string.IsNullOrWhiteSpace(_target.ProcessName)
        ? T("QuickFill.Popup.UnknownTarget")
        : string.IsNullOrWhiteSpace(_target.WindowTitle)
            ? _target.ProcessName
            : $"{_target.ProcessName} - {SafeWindowTitle(_target.WindowTitle)}";

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsBrowsingEntries));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    partial void OnIsEditingEntryChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBrowsingEntries));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnShowAllForAppChanged(bool value)
    {
        OnPropertyChanged(nameof(MatchScopeLabel));
        if (IsBrowsingEntries)
            _ = LoadMatchesAsync();
    }

    partial void OnSelectedApiKeyOptionChanged(QuickFillLinkedItemOption? value) => RefreshApiKeyFieldOptions(value?.Id);

    public async Task LoadAsync()
    {
        IsBusy = true;
        Status = "";
        try
        {
            IsLocked = !_root.IsUnlocked;
            if (IsLocked)
            {
                LoadVaultChoices();
                return;
            }

            await LoadSourceListsAsync();
            await LoadMatchesAsync();
            _root.LogActivity("quick-fill", "Quick Fill opened", "Opened Quick Fill popup.", "info", _root.VaultPath);
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

    [RelayCommand]
    private void ToggleMatchScope()
    {
        ShowAllForApp = !ShowAllForApp;
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (SelectedVault is null)
        {
            Status = T("QuickFill.Popup.Error.SelectVault");
            return;
        }

        var error = await _root.UnlockFromQuickFillAsync(SelectedVault.VaultPath, MasterPassword);
        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = error;
            return;
        }

        MasterPassword = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task FillSelectedAsync()
    {
        if (SelectedEntry is null)
            return;

        if (!_root.QuickFill.HasAutoTypeAcknowledgement)
        {
            var confirmed = await _root.ConfirmAsync(
                T("QuickFill.AutoType.WarningTitle"),
                T("QuickFill.AutoType.WarningDetail"),
                T("QuickFill.AutoType.AllowButton"));
            if (!confirmed)
                return;

            _root.AcceptQuickFillAutoTypeAcknowledgement();
        }

        var entry = SelectedEntry.Entry;
        _root.LogActivity("quick-fill", "Quick Fill fill attempted", $"Attempted Quick Fill for {entry.Name}.", "info", _root.VaultPath, entry.Name);
        using var _ = _root.SuppressTransientFocusLoss();
        var steps = await BuildAutoTypeStepsAsync(entry);
        if (steps.Count == 0)
        {
            Status = T("QuickFill.Status.NoSecretResolved");
            return;
        }

        var sent = await _autoTypeService.SendAsync(_target.WindowHandle, steps);
        if (sent)
        {
            _root.LogActivity("quick-fill", "Quick Fill completed", $"Completed Quick Fill for {entry.Name}.", "success", _root.VaultPath, entry.Name);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _root.LogActivity("quick-fill", "Quick Fill aborted", $"Aborted Quick Fill for {entry.Name}.", "warning", _root.VaultPath, entry.Name);
            Status = T("QuickFill.Status.AutoTypeAborted");
        }
    }

    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        if (SelectedEntry is null)
            return;

        var value = await ResolvePrimarySecretAsync(SelectedEntry.Entry);
        if (string.IsNullOrEmpty(value))
        {
            Status = T("QuickFill.Status.NoSecretResolved");
            return;
        }

        await _root.CopyToClipboardAsync(value);
        Status = T(_root.QuickFill.HasAutoTypeAcknowledgement ? "QuickFill.Status.Copied" : "QuickFill.Status.CopiedClipboardOnly");
    }

    [RelayCommand]
    private void StartAddEntry()
    {
        _editingEntry = null;
        IsEditingEntry = true;
        EntryName = string.IsNullOrWhiteSpace(_target.ProcessName) ? T("QuickFill.Editor.NewEntry") : _target.ProcessName;
        EntryCategory = "Other";
        EntryEnabled = true;
        TargetProcessName = _target.ProcessName;
        TargetWindowTitleContains = "";
        PressEnterAfterFill = false;
        Notes = "";
        Fields.Clear();
        ResetAddFieldInputs();
        Status = "";
        OnPropertyChanged(nameof(HasFields));
    }

    [RelayCommand]
    private void StartEditEntry()
    {
        if (SelectedEntry is null)
            return;

        _editingEntry = SelectedEntry;
        IsEditingEntry = true;
        PopulateEditor(SelectedEntry.Entry);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditingEntry = false;
        _editingEntry = null;
        Status = "";
    }

    [RelayCommand]
    private void AddOwnedField()
    {
        var kind = SelectedOwnedFieldKind?.Kind ?? QuickFillFieldKind.Text;
        var label = string.IsNullOrWhiteSpace(OwnedFieldLabel) ? DefaultFieldLabel(kind) : OwnedFieldLabel.Trim();
        Fields.Add(QuickFillFieldEditorVm.FromField(new QuickFillField(
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
        OnPropertyChanged(nameof(HasFields));
    }

    [RelayCommand]
    private void AddWebLoginField()
    {
        if (SelectedWebLoginOption is null || SelectedWebLoginFieldOption is null)
            return;

        Fields.Add(QuickFillFieldEditorVm.FromField(new QuickFillField(
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
        OnPropertyChanged(nameof(HasFields));
    }

    [RelayCommand]
    private void AddApiKeyField()
    {
        if (SelectedApiKeyOption is null || SelectedApiKeyFieldOption is null)
            return;

        Fields.Add(QuickFillFieldEditorVm.FromField(new QuickFillField(
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
        OnPropertyChanged(nameof(HasFields));
    }

    [RelayCommand]
    private void AddAuthenticatorField()
    {
        if (SelectedAuthenticatorOption is null)
            return;

        Fields.Add(QuickFillFieldEditorVm.FromField(new QuickFillField(
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
        OnPropertyChanged(nameof(HasFields));
    }

    [RelayCommand]
    private void RemoveField(QuickFillFieldEditorVm? field)
    {
        if (field is null)
            return;

        Fields.Remove(field);
        ResequenceFields();
        OnPropertyChanged(nameof(HasFields));
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
            if (_editingEntry is null)
            {
                saved = await _entryService.AddAsync(_root.VaultPath, _root.VaultKey, input);
                _root.LogActivity("quick-fill", "Quick Fill entry created", $"Created Quick Fill entry {saved.Name}.", "success", _root.VaultPath, saved.Name);
            }
            else
            {
                saved = await _entryService.UpdateAsync(_root.VaultPath, _root.VaultKey, _editingEntry.Entry.Id, _editingEntry.Entry.CreatedAtUtc, input);
                _root.LogActivity("quick-fill", "Quick Fill entry updated", $"Updated Quick Fill entry {saved.Name}.", "success", _root.VaultPath, saved.Name);
            }

            IsEditingEntry = false;
            _editingEntry = null;
            await LoadMatchesAsync();
            SelectedEntry = Matches.FirstOrDefault(entry => entry.Entry.Id == saved.Id) ?? Matches.FirstOrDefault();
            Status = T("QuickFill.Status.Saved", saved.Name);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void LoadVaultChoices()
    {
        VaultChoices.Clear();
        foreach (var vault in _vaultRegistryService.ListVaults())
            VaultChoices.Add(new VaultChoiceVm(vault.DisplayName, vault.VaultPath, File.Exists(vault.VaultPath)));

        SelectedVault = VaultChoices.FirstOrDefault(choice => choice.Exists) ?? VaultChoices.FirstOrDefault();
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private async Task LoadSourceListsAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        _webLogins = await _webLoginService.ListAsync(_root.VaultPath, _root.VaultKey);
        _apiKeys = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
        _authenticators = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
        RefreshLinkedOptions();
    }

    private async Task LoadMatchesAsync()
    {
        Matches.Clear();
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entries = await _entryService.ListAsync(_root.VaultPath, _root.VaultKey);
        var matched = entries.Where(entry => ShowAllForApp
            ? QuickFillMatcher.IsProcessMatch(entry, _target)
            : QuickFillMatcher.IsMatch(entry, _target));

        foreach (var entry in matched.OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            Matches.Add(new QuickFillPopupEntryVm(entry));

        SelectedEntry = Matches.FirstOrDefault();
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private async Task<IReadOnlyList<AutoTypeStep>> BuildAutoTypeStepsAsync(QuickFillEntry entry)
    {
        var values = new List<string>();
        var fields = QuickFillSequencePreviewer.ChooseFillFields(entry);
        foreach (var field in fields)
        {
            var value = await ResolveFieldAsync(field);
            if (!string.IsNullOrEmpty(value))
                values.Add(value);
        }

        if (values.Count == 0)
            return Array.Empty<AutoTypeStep>();

        var steps = new List<AutoTypeStep>();
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                steps.Add(new AutoTypeStep(AutoTypeStepKind.Tab));
            steps.Add(new AutoTypeStep(AutoTypeStepKind.Text, values[i]));
        }

        if (entry.PressEnterAfterFill)
            steps.Add(new AutoTypeStep(AutoTypeStepKind.Enter));

        return steps;
    }

    private async Task<string> ResolvePrimarySecretAsync(QuickFillEntry entry)
    {
        var field = entry.Fields
            .OrderBy(field => field.SortOrder)
            .FirstOrDefault(field => field.Kind is QuickFillFieldKind.Password or QuickFillFieldKind.Secret or QuickFillFieldKind.Otp)
            ?? entry.Fields.OrderBy(field => field.SortOrder).FirstOrDefault();

        return field is null ? "" : await ResolveFieldAsync(field);
    }

    private Task<string> ResolveFieldAsync(QuickFillField field)
        => Task.FromResult(field.SourceKind switch
        {
            QuickFillFieldSourceKind.Owned => field.Value,
            QuickFillFieldSourceKind.WebLogin => ResolveWebLoginField(field.LinkedItemId, field.LinkedFieldName),
            QuickFillFieldSourceKind.ApiKeyField => ResolveApiKeyField(field.LinkedItemId, field.LinkedFieldId),
            QuickFillFieldSourceKind.Authenticator => ResolveAuthenticator(field.LinkedItemId),
            _ => ""
        });

    private string ResolveWebLoginField(string itemId, string fieldName)
    {
        var login = _webLogins.FirstOrDefault(entry => entry.Id == itemId);
        if (login is null)
            return "";

        return fieldName.Trim().ToLowerInvariant() switch
        {
            "username" => login.Username,
            "email" => login.Email,
            "password" => login.Password,
            "url" => login.Url,
            _ => ""
        };
    }

    private string ResolveApiKeyField(string itemId, string fieldId)
    {
        var apiKey = _apiKeys.FirstOrDefault(entry => entry.Id == itemId);
        return apiKey?.Fields.FirstOrDefault(field => field.Id == fieldId)?.Value ?? "";
    }

    private string ResolveAuthenticator(string itemId)
    {
        var entry = _authenticators.FirstOrDefault(entry => entry.Id == itemId);
        return entry is null ? "" : _authenticatorService.GetCurrentCode(entry).Code;
    }

    private QuickFillEntryInput BuildInput()
    {
        ResequenceFields();
        return new QuickFillEntryInput(
            EntryName,
            EntryCategory,
            EntryEnabled,
            new QuickFillTargetRule(TargetProcessName, TargetWindowTitleContains),
            Fields.Select(field => field.ToField()).ToArray(),
            PressEnterAfterFill,
            Notes);
    }

    private void PopulateEditor(QuickFillEntry entry)
    {
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
        ResetAddFieldInputs();
        OnPropertyChanged(nameof(HasFields));
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

    private void ResequenceFields()
    {
        var order = 0;
        foreach (var field in Fields)
            field.SortOrder = order++;
    }

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

    private void RefreshOptionLocalization()
    {
        foreach (var option in FieldKindOptions)
            option.RefreshLocalization(_root);
        foreach (var option in WebLoginFieldOptions)
            option.RefreshLocalization(_root);
    }

    private static string SafeWindowTitle(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 44 ? trimmed : $"{trimmed[..44]}...";
    }

    private static string SafeTargetName(QuickFillTargetContext target)
        => string.IsNullOrWhiteSpace(target.ProcessName) ? "app" : target.ProcessName;

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
}

public sealed class QuickFillPopupEntryVm
{
    public QuickFillPopupEntryVm(QuickFillEntry entry)
    {
        Entry = entry;
    }

    public QuickFillEntry Entry { get; }
    public string Name => Entry.Name;
    public string Category => Entry.Category;
    public string TargetDisplay => string.IsNullOrWhiteSpace(Entry.Target.WindowTitleContains)
        ? Entry.Target.ProcessName
        : $"{Entry.Target.ProcessName} / {Entry.Target.WindowTitleContains}";
    public string FieldPreview => string.Join(" -> ", QuickFillSequencePreviewer.BuildPreview(Entry));
}

public sealed class VaultChoiceVm
{
    public VaultChoiceVm(string displayName, string vaultPath, bool exists)
    {
        DisplayName = displayName;
        VaultPath = vaultPath;
        Exists = exists;
    }

    public string DisplayName { get; }
    public string VaultPath { get; }
    public bool Exists { get; }
    public string Status => Exists ? "Available" : "Missing";
    public override string ToString() => DisplayName;
}

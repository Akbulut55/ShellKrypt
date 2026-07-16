using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Application.QuickFill;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services.QuickFill;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.Features.QuickFill;

namespace ShellKrypt.Desktop.Features.QuickFill;

public sealed partial class QuickFillPopupViewModel : ViewModelBase
{
    private readonly QuickFillRuntime _root;
    private readonly IDesktopNavigation _navigation;
    private readonly IVaultService _vaultService;
    private readonly SessionSecurityService _sessionSecurity;
    private readonly VaultRegistryService _vaultRegistryService;
    private readonly IQuickFillEntryService _entryService;
    private readonly IWebLoginService _webLoginService;
    private readonly ICardService _cardService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IAuthenticatorEntryService _authenticatorService;
    private readonly IOneTimePasswordGenerator _oneTimePasswordGenerator;
    private readonly AutoTypeService _autoTypeService;
    private readonly QuickFillTargetContext _target;

    private QuickFillPopupEntryVm? _editingEntry;
    private IReadOnlyList<WebLoginEntry> _webLogins = [];
    private IReadOnlyList<CardEntry> _creditCards = [];
    private IReadOnlyList<ApiKeyEntry> _apiKeys = [];
    private IReadOnlyList<AuthenticatorEntry> _authenticators = [];

    [ObservableProperty] private bool isLocked;
    [ObservableProperty] private bool isEditingEntry;
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
    [ObservableProperty] private VaultChoiceVm? selectedVault;
    [ObservableProperty] private QuickFillPopupEntryVm? selectedEntry;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool showAllForApp;

    public QuickFillPopupViewModel(
        QuickFillRuntime root,
        IDesktopNavigation navigation,
        SessionSecurityService sessionSecurity,
        VaultRegistryService vaultRegistryService,
        IVaultService vaultService,
        IQuickFillEntryService entryService,
        IWebLoginService webLoginService,
        ICardService cardService,
        IApiKeyService apiKeyService,
        IAuthenticatorEntryService authenticatorService,
        IOneTimePasswordGenerator oneTimePasswordGenerator,
        AutoTypeService autoTypeService,
        QuickFillTargetContext target)
    {
        _root = root;
        _navigation = navigation;
        _sessionSecurity = sessionSecurity;
        _vaultService = vaultService;
        _vaultRegistryService = vaultRegistryService;
        _entryService = entryService;
        _webLoginService = webLoginService;
        _cardService = cardService;
        _apiKeyService = apiKeyService;
        _authenticatorService = authenticatorService;
        _oneTimePasswordGenerator = oneTimePasswordGenerator;
        _autoTypeService = autoTypeService;
        _target = target;

        Editor = new QuickFillEntryEditorVm(T)
        {
            SaveRequested = SaveEditorEntryAsync,
            DeleteRequested = DeleteEditorEntryAsync,
            CancelRequested = CancelEditorEdit
        };
    }

    public event EventHandler? CloseRequested;

    public QuickFillEntryEditorVm Editor { get; }
    public ObservableCollection<VaultChoiceVm> VaultChoices { get; } = new();
    public ObservableCollection<QuickFillPopupEntryVm> Matches { get; } = new();

    public bool IsUnlocked => !IsLocked;
    public bool IsBrowsingEntries => IsUnlocked && !IsEditingEntry;
    public bool ShowHeaderStatus => HasStatus && !IsLocked;
    public bool HasMatches => IsBrowsingEntries && Matches.Count > 0;
    public bool HasNoMatches => IsBrowsingEntries && Matches.Count == 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool CanFillSelected => SelectedEntry is not null && _target.WindowHandle != 0 && !string.IsNullOrWhiteSpace(_target.ProcessName);
    public bool IsRestrictedTargetMode => string.IsNullOrWhiteSpace(_target.ProcessName) || _target.WindowHandle == 0;
    public string MatchScopeLabel => ShowAllForApp
        ? T("QuickFill.Popup.Scope.Window")
        : T("QuickFill.Popup.Scope.App", SafeTargetName(_target));
    public string TargetDisplay => string.IsNullOrWhiteSpace(_target.ProcessName)
        ? T("QuickFill.Popup.UnknownTarget")
        : string.IsNullOrWhiteSpace(_target.WindowTitle)
            ? _target.ProcessName
            : $"{_target.ProcessName} - {SafeWindowTitle(_target.WindowTitle)}";
    public string PasswordVisibilityLabel => ShowPassword ? T("Common.Hide") : T("Common.Show");

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsBrowsingEntries));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(ShowHeaderStatus));
    }

    partial void OnIsEditingEntryChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBrowsingEntries));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(ShowHeaderStatus));
    }

    partial void OnSelectedEntryChanged(QuickFillPopupEntryVm? value) => OnPropertyChanged(nameof(CanFillSelected));
    partial void OnShowPasswordChanged(bool value) => OnPropertyChanged(nameof(PasswordVisibilityLabel));
    partial void OnShowAllForAppChanged(bool value)
    {
        OnPropertyChanged(nameof(MatchScopeLabel));
        if (IsBrowsingEntries)
            _ = LoadMatchesAsync();
    }

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

    public override void RefreshLocalization()
    {
        Editor.RefreshLocalization();
        NotifyLocalized(nameof(MatchScopeLabel));
        NotifyLocalized(nameof(TargetDisplay));
        NotifyLocalized(nameof(PasswordVisibilityLabel));
    }

    [RelayCommand]
    private void ToggleMatchScope() => ShowAllForApp = !ShowAllForApp;

    [RelayCommand]
    private void TogglePasswordVisibility() => ShowPassword = !ShowPassword;

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (SelectedVault is null)
        {
            Status = T("QuickFill.Popup.Error.SelectVault");
            return;
        }

        var error = await UnlockAsync(SelectedVault.VaultPath, MasterPassword);
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
        if (!CanFillSelected || SelectedEntry is null)
        {
            Status = T("QuickFill.Status.AutoTypeUnavailableWayland");
            return;
        }

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
        using var _ = _sessionSecurity.SuppressTransientFocusLoss();
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
        Editor.CanDeleteEntry = false;
        Editor.PrepareFromTarget(new QuickFillTargetContext(_target.ProcessName, "", _target.WindowHandle));
        Status = "";
    }

    [RelayCommand]
    private void StartEditEntry()
    {
        if (SelectedEntry is null)
            return;

        _editingEntry = SelectedEntry;
        IsEditingEntry = true;
        Editor.CanDeleteEntry = true;
        Editor.Populate(SelectedEntry.Entry);
    }

    [RelayCommand]
    private async Task SetEntryEnabledAsync(QuickFillPopupEntryVm? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entry = row.Entry;
        var input = ToInput(entry, row.IsEnabled);
        var updated = await _entryService.UpdateAsync(_root.VaultPath, _root.VaultKey, entry.Id, entry.CreatedAtUtc, input);
        _root.LogActivity("quick-fill", "Quick Fill entry updated", $"Updated Quick Fill entry {updated.Name}.", "success", _root.VaultPath, updated.Name);
        await LoadMatchesAsync();
        SelectedEntry = Matches.FirstOrDefault(match => match.Entry.Id == updated.Id) ?? Matches.FirstOrDefault();
        Status = T("QuickFill.Status.Saved", updated.Name);
    }

    [RelayCommand]
    private async Task DeleteEntryRowAsync(QuickFillPopupEntryVm? row)
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
        await LoadMatchesAsync();
        Status = T("QuickFill.Status.Deleted", entryName);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

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
            Editor.CanDeleteEntry = false;
            await LoadMatchesAsync();
            SelectedEntry = Matches.FirstOrDefault(entry => entry.Entry.Id == saved.Id) ?? Matches.FirstOrDefault();
            Status = T("QuickFill.Status.Saved", saved.Name);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async Task DeleteEditorEntryAsync()
    {
        if (_editingEntry is null || string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entryName = _editingEntry.Entry.Name;
        var confirmed = await _root.ConfirmAsync(
            T("QuickFill.Delete.Title"),
            T("QuickFill.Delete.Subtitle", entryName),
            T("Common.Delete"),
            destructive: true);
        if (!confirmed)
            return;

        await _entryService.DeleteAsync(_root.VaultPath, _editingEntry.Entry.Id);
        _root.LogActivity("quick-fill", "Quick Fill entry deleted", $"Deleted Quick Fill entry {entryName}.", "warning", _root.VaultPath, entryName);

        IsEditingEntry = false;
        _editingEntry = null;
        Editor.CanDeleteEntry = false;
        await LoadMatchesAsync();
        Status = T("QuickFill.Status.Deleted", entryName);
    }

    private void CancelEditorEdit()
    {
        IsEditingEntry = false;
        _editingEntry = null;
        Editor.CanDeleteEntry = false;
        Status = "";
    }

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
        _creditCards = await _cardService.ListAsync(_root.VaultPath, _root.VaultKey);
        _apiKeys = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
        _authenticators = await _authenticatorService.ListAsync(_root.VaultPath, _root.VaultKey);
        Editor.SetLinkedSources(_webLogins, _creditCards, _apiKeys, _authenticators);
    }

    private async Task LoadMatchesAsync()
    {
        Matches.Clear();
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        var entries = await _entryService.ListAsync(_root.VaultPath, _root.VaultKey);
        var matched = IsRestrictedTargetMode
            ? entries.Where(entry => entry.Enabled)
            : entries.Where(entry => ShowAllForApp
                ? QuickFillMatcher.IsProcessMatch(entry, _target)
                : QuickFillMatcher.IsMatch(entry, _target));

        foreach (var entry in matched.OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            Matches.Add(new QuickFillPopupEntryVm(entry));

        SelectedEntry = Matches.FirstOrDefault();
        if (IsRestrictedTargetMode && Matches.Count > 0)
            Status = T("QuickFill.Status.WaylandRestricted");
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(CanFillSelected));
    }

    private async Task<IReadOnlyList<AutoTypeStep>> BuildAutoTypeStepsAsync(QuickFillEntry entry)
    {
        var steps = new List<AutoTypeStep>();
        foreach (var sequenceStep in QuickFillSequencePreviewer.NormalizeSequenceSteps(entry).OrderBy(step => step.SortOrder))
        {
            switch (sequenceStep.Kind)
            {
                case QuickFillSequenceStepKind.Field:
                    var field = entry.Fields.FirstOrDefault(field => string.Equals(field.Id, sequenceStep.FieldId, StringComparison.OrdinalIgnoreCase));
                    if (field is not null)
                    {
                        var value = await ResolveFieldAsync(field);
                        if (!string.IsNullOrEmpty(value))
                            steps.Add(new AutoTypeStep(AutoTypeStepKind.Text, value));
                    }
                    break;
                case QuickFillSequenceStepKind.Keystroke:
                    steps.Add(new AutoTypeStep(
                        AutoTypeStepKind.Key,
                        Key: sequenceStep.Keystroke,
                        Modifiers: sequenceStep.Modifiers,
                        RepeatCount: sequenceStep.RepeatCount));
                    break;
                case QuickFillSequenceStepKind.LiteralText:
                    if (!string.IsNullOrEmpty(sequenceStep.Text))
                        steps.Add(new AutoTypeStep(AutoTypeStepKind.Text, sequenceStep.Text));
                    break;
                case QuickFillSequenceStepKind.Delay:
                    steps.Add(new AutoTypeStep(AutoTypeStepKind.Delay, DelayMilliseconds: Math.Clamp(sequenceStep.DelayMilliseconds, 0, 10_000)));
                    break;
            }
        }

        if (steps.Count == 0)
            return Array.Empty<AutoTypeStep>();

        if (entry.PressEnterAfterFill)
            steps.Add(new AutoTypeStep(AutoTypeStepKind.Key, Key: QuickFillKeystrokeKind.Enter));

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
            QuickFillFieldSourceKind.CreditCard => ResolveCreditCardField(field.LinkedItemId, field.LinkedFieldName),
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

    private string ResolveCreditCardField(string itemId, string fieldName)
    {
        var card = _creditCards.FirstOrDefault(entry => entry.Id == itemId);
        if (card is null)
            return "";

        return fieldName.Trim().ToLowerInvariant() switch
        {
            "cardholder" => card.Cardholder,
            "number" => card.Number,
            "expiry_month" => card.ExpiryMonth.ToString("00"),
            "expiry_year" => card.ExpiryYear.ToString("0000"),
            "expiry" => $"{card.ExpiryMonth:00}/{card.ExpiryYear:0000}",
            "cvc" => card.Cvc,
            "bank" => card.Bank,
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
        return entry is null ? "" : _oneTimePasswordGenerator.GetCurrentCode(entry).Code;
    }

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

    private static string SafeWindowTitle(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 44 ? trimmed : $"{trimmed[..44]}...";
    }

    private static string SafeTargetName(QuickFillTargetContext target)
        => string.IsNullOrWhiteSpace(target.ProcessName) ? "app" : target.ProcessName;

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private async Task<string?> UnlockAsync(string vaultPath, string masterPassword)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
            return T("QuickFill.Popup.Error.SelectVault");
        if (string.IsNullOrWhiteSpace(masterPassword))
            return T("QuickFill.Popup.Error.EnterPassword");

        var targetPath = Path.GetFullPath(vaultPath);
        if (_root.IsUnlocked && !string.Equals(_root.VaultPath, targetPath, StringComparison.OrdinalIgnoreCase))
            _navigation.Lock();

        _root.SetVaultPath(targetPath);
        var result = await _vaultService.UnlockAsync(targetPath, masterPassword);
        if (!result.Success || result.VaultKey is null)
            return result.Error ?? T("QuickFill.Popup.Error.UnlockFailed");

        _navigation.OnUnlocked(result.VaultKey);
        return null;
    }
}

public sealed partial class QuickFillPopupEntryVm : ObservableObject
{
    public QuickFillPopupEntryVm(QuickFillEntry entry)
    {
        Entry = entry;
        isEnabled = entry.Enabled;
    }

    public QuickFillEntry Entry { get; }
    [ObservableProperty] private bool isEnabled;
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

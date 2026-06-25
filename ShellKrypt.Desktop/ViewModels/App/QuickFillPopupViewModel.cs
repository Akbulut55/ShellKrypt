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
    private readonly ICardService _cardService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IAuthenticatorService _authenticatorService;
    private readonly AutoTypeService _autoTypeService;
    private readonly QuickFillTargetContext _target;

    private QuickFillPopupEntryVm? _editingEntry;
    private IReadOnlyList<WebLoginEntry> _webLogins = Array.Empty<WebLoginEntry>();
    private IReadOnlyList<CardEntry> _creditCards = Array.Empty<CardEntry>();
    private IReadOnlyList<ApiKeyEntry> _apiKeys = Array.Empty<ApiKeyEntry>();
    private IReadOnlyList<AuthenticatorEntry> _authenticators = Array.Empty<AuthenticatorEntry>();

    [ObservableProperty] private bool isLocked;
    [ObservableProperty] private bool isEditingEntry;
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
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
    [ObservableProperty] private string addStepText = "";
    [ObservableProperty] private int addStepDelayMilliseconds = 250;
    [ObservableProperty] private bool isAddStepModalOpen;
    [ObservableProperty] private bool hasPendingKeyStep;
    [ObservableProperty] private QuickFillKeystrokeKind pendingKeyStep = QuickFillKeystrokeKind.Tab;
    [ObservableProperty] private QuickFillKeyModifiers pendingKeyModifiers = QuickFillKeyModifiers.None;
    [ObservableProperty] private QuickFillAddStepMode addStepMode = QuickFillAddStepMode.Field;
    [ObservableProperty] private QuickFillAddFieldSource addFieldSource = QuickFillAddFieldSource.Manual;
    [ObservableProperty] private bool showAllForApp;
    [ObservableProperty] private QuickFillFieldKindOption? selectedOwnedFieldKind;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedWebLoginOption;
    [ObservableProperty] private QuickFillWebLoginFieldOption? selectedWebLoginFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedCreditCardOption;
    [ObservableProperty] private QuickFillCreditCardFieldOption? selectedCreditCardFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedAuthenticatorOption;
    [ObservableProperty] private QuickFillSequenceStepEditorVm? selectedSequenceStep;

    public QuickFillPopupViewModel(
        MainWindowViewModel root,
        VaultRegistryService vaultRegistryService,
        IVaultService vaultService,
        IQuickFillEntryService entryService,
        IWebLoginService webLoginService,
        ICardService cardService,
        IApiKeyService apiKeyService,
        IAuthenticatorService authenticatorService,
        AutoTypeService autoTypeService,
        QuickFillTargetContext target)
    {
        _root = root;
        _vaultRegistryService = vaultRegistryService;
        _entryService = entryService;
        _webLoginService = webLoginService;
        _cardService = cardService;
        _apiKeyService = apiKeyService;
        _authenticatorService = authenticatorService;
        _autoTypeService = autoTypeService;
        _target = target;

        foreach (var option in CreateFieldKindOptions())
            FieldKindOptions.Add(option);
        foreach (var option in CreateWebLoginFieldOptions())
            WebLoginFieldOptions.Add(option);
        foreach (var option in CreateCreditCardFieldOptions())
            CreditCardFieldOptions.Add(option);
        foreach (var option in CreateSequenceStepKindOptions())
            SequenceStepKindOptions.Add(option);
        foreach (var option in CreateKeystrokeOptions())
            KeystrokeOptions.Add(option);
        RefreshOptionLocalization();
        SelectedOwnedFieldKind = FieldKindOptions.FirstOrDefault();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
        SelectedCreditCardFieldOption = CreditCardFieldOptions.FirstOrDefault();
    }

    public event EventHandler? CloseRequested;

    public ObservableCollection<VaultChoiceVm> VaultChoices { get; } = new();
    public ObservableCollection<QuickFillPopupEntryVm> Matches { get; } = new();
    public ObservableCollection<QuickFillFieldEditorVm> Fields { get; } = new();
    public ObservableCollection<QuickFillFieldKindOption> FieldKindOptions { get; } = new();
    public ObservableCollection<QuickFillWebLoginFieldOption> WebLoginFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> WebLoginOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> CreditCardOptions { get; } = new();
    public ObservableCollection<QuickFillCreditCardFieldOption> CreditCardFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> AuthenticatorOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceStepEditorVm> SequenceSteps { get; } = new();
    public ObservableCollection<QuickFillSequenceStepKindOption> SequenceStepKindOptions { get; } = new();
    public ObservableCollection<QuickFillKeystrokeOption> KeystrokeOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceFieldOption> SequenceFieldOptions { get; } = new();

    public bool IsUnlocked => !IsLocked;
    public bool IsBrowsingEntries => IsUnlocked && !IsEditingEntry;
    public bool ShowHeaderStatus => HasStatus && !IsLocked;
    public bool HasMatches => IsBrowsingEntries && Matches.Count > 0;
    public bool HasNoMatches => IsBrowsingEntries && Matches.Count == 0;
    public bool HasFields => Fields.Count > 0;
    public bool HasSequenceSteps => SequenceSteps.Count > 0;
    public bool HasNoSequenceSteps => SequenceSteps.Count == 0;
    public bool IsAddFieldMode => AddStepMode == QuickFillAddStepMode.Field;
    public bool IsAddKeyMode => AddStepMode == QuickFillAddStepMode.Key;
    public bool IsAddTextMode => AddStepMode == QuickFillAddStepMode.Text;
    public bool IsAddDelayMode => AddStepMode == QuickFillAddStepMode.Delay;
    public bool CanCaptureQuickFillKey => IsAddStepModalOpen && IsAddKeyMode;
    public string PendingKeyPreviewText => !IsAddKeyMode
        ? ""
        : HasPendingKeyStep
        ? T("QuickFill.Sequence.PendingKeyPreview", QuickFillKeyDisplayFormatter.Format(PendingKeyStep, PendingKeyModifiers))
        : T("QuickFill.Sequence.PendingKeyEmpty");
    public bool IsManualFieldSource => AddFieldSource == QuickFillAddFieldSource.Manual;
    public bool IsWebLoginFieldSource => AddFieldSource == QuickFillAddFieldSource.WebLogin;
    public bool IsCreditCardFieldSource => AddFieldSource == QuickFillAddFieldSource.CreditCard;
    public bool IsApiKeyFieldSource => AddFieldSource == QuickFillAddFieldSource.ApiKey;
    public bool IsAuthenticatorFieldSource => AddFieldSource == QuickFillAddFieldSource.Authenticator;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool CanDeleteEntry => _editingEntry is not null;
    public bool CanFillSelected => SelectedEntry is not null && _target.WindowHandle != 0 && !string.IsNullOrWhiteSpace(_target.ProcessName);
    public bool IsRestrictedTargetMode => string.IsNullOrWhiteSpace(_target.ProcessName) || _target.WindowHandle == 0;
    public bool HasTargetWindowTitleContains => !string.IsNullOrWhiteSpace(TargetWindowTitleContains);
    public string TargetProcessDisplay => string.IsNullOrWhiteSpace(TargetProcessName)
        ? T("QuickFill.Editor.TargetNotSet")
        : TargetProcessName;
    public string SequenceStepCountText => T("QuickFill.Sequence.ConfiguredCount", SequenceSteps.Count);
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
    partial void OnTargetProcessNameChanged(string value) => OnPropertyChanged(nameof(TargetProcessDisplay));
    partial void OnTargetWindowTitleContainsChanged(string value) => OnPropertyChanged(nameof(HasTargetWindowTitleContains));
    partial void OnShowAllForAppChanged(bool value)
    {
        OnPropertyChanged(nameof(MatchScopeLabel));
        if (IsBrowsingEntries)
            _ = LoadMatchesAsync();
    }

    partial void OnSelectedApiKeyOptionChanged(QuickFillLinkedItemOption? value) => RefreshApiKeyFieldOptions(value?.Id);
    partial void OnAddStepModeChanged(QuickFillAddStepMode value)
    {
        OnPropertyChanged(nameof(IsAddFieldMode));
        OnPropertyChanged(nameof(IsAddKeyMode));
        OnPropertyChanged(nameof(IsAddTextMode));
        OnPropertyChanged(nameof(IsAddDelayMode));
        OnPropertyChanged(nameof(CanCaptureQuickFillKey));
        OnPropertyChanged(nameof(PendingKeyPreviewText));
        if (value != QuickFillAddStepMode.Key)
            ClearPendingKeyStep();
    }
    partial void OnIsAddStepModalOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCaptureQuickFillKey));
        if (!value)
            ClearPendingKeyStep();
    }
    partial void OnHasPendingKeyStepChanged(bool value) => OnPropertyChanged(nameof(PendingKeyPreviewText));
    partial void OnPendingKeyStepChanged(QuickFillKeystrokeKind value) => OnPropertyChanged(nameof(PendingKeyPreviewText));
    partial void OnPendingKeyModifiersChanged(QuickFillKeyModifiers value) => OnPropertyChanged(nameof(PendingKeyPreviewText));
    partial void OnAddFieldSourceChanged(QuickFillAddFieldSource value)
    {
        OnPropertyChanged(nameof(IsManualFieldSource));
        OnPropertyChanged(nameof(IsWebLoginFieldSource));
        OnPropertyChanged(nameof(IsCreditCardFieldSource));
        OnPropertyChanged(nameof(IsApiKeyFieldSource));
        OnPropertyChanged(nameof(IsAuthenticatorFieldSource));
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

    [RelayCommand]
    private void ToggleMatchScope()
    {
        ShowAllForApp = !ShowAllForApp;
    }

    [RelayCommand]
    private void OpenAddStepModal()
    {
        AddStepMode = QuickFillAddStepMode.Field;
        AddFieldSource = QuickFillAddFieldSource.Manual;
        ClearPendingKeyStep();
        IsAddStepModalOpen = true;
    }

    [RelayCommand]
    private void CloseAddStepModal()
    {
        ClearPendingKeyStep();
        IsAddStepModalOpen = false;
    }

    [RelayCommand]
    private void SelectAddFieldMode() => AddStepMode = QuickFillAddStepMode.Field;

    [RelayCommand]
    private void SelectAddKeyMode() => AddStepMode = QuickFillAddStepMode.Key;

    [RelayCommand]
    private void SelectAddTextMode() => AddStepMode = QuickFillAddStepMode.Text;

    [RelayCommand]
    private void SelectAddDelayMode() => AddStepMode = QuickFillAddStepMode.Delay;

    [RelayCommand]
    private void SelectManualFieldSource() => AddFieldSource = QuickFillAddFieldSource.Manual;

    [RelayCommand]
    private void SelectWebLoginFieldSource() => AddFieldSource = QuickFillAddFieldSource.WebLogin;

    [RelayCommand]
    private void SelectCreditCardFieldSource() => AddFieldSource = QuickFillAddFieldSource.CreditCard;

    [RelayCommand]
    private void SelectApiKeyFieldSource() => AddFieldSource = QuickFillAddFieldSource.ApiKey;

    [RelayCommand]
    private void SelectAuthenticatorFieldSource() => AddFieldSource = QuickFillAddFieldSource.Authenticator;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        ShowPassword = !ShowPassword;
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
        OnPropertyChanged(nameof(CanDeleteEntry));
        IsEditingEntry = true;
        EntryName = string.IsNullOrWhiteSpace(_target.ProcessName) ? T("QuickFill.Editor.NewEntry") : _target.ProcessName;
        EntryCategory = "Other";
        EntryEnabled = true;
        TargetProcessName = _target.ProcessName;
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
        OnPropertyChanged(nameof(HasNoSequenceSteps));
    }

    [RelayCommand]
    private void StartEditEntry()
    {
        if (SelectedEntry is null)
            return;

        _editingEntry = SelectedEntry;
        OnPropertyChanged(nameof(CanDeleteEntry));
        IsEditingEntry = true;
        PopulateEditor(SelectedEntry.Entry);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditingEntry = false;
        _editingEntry = null;
        OnPropertyChanged(nameof(CanDeleteEntry));
        Status = "";
    }

    [RelayCommand]
    private void AddOwnedField()
    {
        var kind = QuickFillFieldKind.Text;
        var label = string.IsNullOrWhiteSpace(OwnedFieldLabel) ? DefaultFieldLabel(kind) : OwnedFieldLabel.Trim();
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
    private void AddCreditCardField()
    {
        if (SelectedCreditCardOption is null || SelectedCreditCardFieldOption is null)
            return;

        AppendFieldAndStep(QuickFillFieldEditorVm.FromField(new QuickFillField(
            Guid.NewGuid().ToString("N"),
            SelectedCreditCardFieldOption.Label,
            SelectedCreditCardFieldOption.Kind,
            SelectedCreditCardFieldOption.IsSensitive,
            Fields.Count,
            QuickFillFieldSourceKind.CreditCard,
            "",
            SelectedCreditCardOption.Id,
            SelectedCreditCardFieldOption.FieldName,
            SelectedCreditCardFieldOption.FieldName)));
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
        OnPropertyChanged(nameof(HasNoSequenceSteps));
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
        => CaptureKeyStep(QuickFillKeystrokeKind.Tab, QuickFillKeyModifiers.None);

    [RelayCommand]
    private void AddEnterSequenceStep()
        => CaptureKeyStep(QuickFillKeystrokeKind.Enter, QuickFillKeyModifiers.None);

    public void AddCapturedKeyStep(QuickFillKeystrokeKind key, QuickFillKeyModifiers modifiers)
        => CaptureKeyStep(key, modifiers);

    [RelayCommand]
    private void ConfirmPendingKeyStep()
    {
        if (!HasPendingKeyStep)
            return;

        AddSequenceStep(new QuickFillSequenceStep(
            Guid.NewGuid().ToString("N"),
            QuickFillSequenceStepKind.Keystroke,
            SequenceSteps.Count,
            "",
            PendingKeyStep,
            "",
            0,
            PendingKeyModifiers,
            1));
        ClearPendingKeyStep();
    }

    [RelayCommand]
    private void ClearPendingKeyStep()
    {
        HasPendingKeyStep = false;
        PendingKeyStep = QuickFillKeystrokeKind.Tab;
        PendingKeyModifiers = QuickFillKeyModifiers.None;
    }

    private void CaptureKeyStep(QuickFillKeystrokeKind key, QuickFillKeyModifiers modifiers)
    {
        PendingKeyStep = key;
        PendingKeyModifiers = modifiers;
        HasPendingKeyStep = true;
    }

    [RelayCommand]
    private void AddTextSequenceStep()
    {
        AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.LiteralText, SequenceSteps.Count, "", QuickFillKeystrokeKind.Tab, AddStepText, 0));
        AddStepText = "";
    }

    [RelayCommand]
    private void AddDelaySequenceStep()
        => AddSequenceStep(new QuickFillSequenceStep(Guid.NewGuid().ToString("N"), QuickFillSequenceStepKind.Delay, SequenceSteps.Count, "", QuickFillKeystrokeKind.Tab, "", Math.Clamp(AddStepDelayMilliseconds, 0, 10_000)));

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
        OnPropertyChanged(nameof(HasNoSequenceSteps));
        OnPropertyChanged(nameof(SequenceStepCountText));
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
            OnPropertyChanged(nameof(CanDeleteEntry));
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
    private async Task DeleteEntryAsync()
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
        OnPropertyChanged(nameof(CanDeleteEntry));
        await LoadMatchesAsync();
        Status = T("QuickFill.Status.Deleted", entryName);
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
        RefreshLinkedOptions();
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
        return entry is null ? "" : _authenticatorService.GetCurrentCode(entry).Code;
    }

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
        RefreshSequenceFieldOptions();
        SequenceSteps.Clear();
        foreach (var step in QuickFillSequencePreviewer.NormalizeSequenceSteps(entry))
            AddSequenceStep(step, notify: false);
        ResetAddFieldInputs();
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasSequenceSteps));
        OnPropertyChanged(nameof(HasNoSequenceSteps));
    }

    private void RefreshLinkedOptions()
    {
        WebLoginOptions.Clear();
        foreach (var login in _webLogins.OrderBy(login => login.Title, StringComparer.OrdinalIgnoreCase))
            WebLoginOptions.Add(new QuickFillLinkedItemOption(login.Id, login.Title));

        CreditCardOptions.Clear();
        foreach (var card in _creditCards.OrderBy(card => card.Title, StringComparer.OrdinalIgnoreCase))
            CreditCardOptions.Add(new QuickFillLinkedItemOption(card.Id, string.IsNullOrWhiteSpace(card.Title) ? card.CardType : card.Title));

        ApiKeyOptions.Clear();
        foreach (var apiKey in _apiKeys.OrderBy(apiKey => apiKey.Name, StringComparer.OrdinalIgnoreCase))
            ApiKeyOptions.Add(new QuickFillLinkedItemOption(apiKey.Id, apiKey.Name));

        AuthenticatorOptions.Clear();
        foreach (var authenticator in _authenticators.OrderBy(authenticator => authenticator.Name, StringComparer.OrdinalIgnoreCase))
            AuthenticatorOptions.Add(new QuickFillLinkedItemOption(authenticator.Id, authenticator.Name));

        SelectedWebLoginOption ??= WebLoginOptions.FirstOrDefault();
        SelectedWebLoginFieldOption ??= WebLoginFieldOptions.FirstOrDefault();
        SelectedCreditCardOption ??= CreditCardOptions.FirstOrDefault();
        SelectedCreditCardFieldOption ??= CreditCardFieldOptions.FirstOrDefault();
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
        SelectedCreditCardOption = CreditCardOptions.FirstOrDefault();
        SelectedCreditCardFieldOption = CreditCardFieldOptions.FirstOrDefault();
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
        {
            OnPropertyChanged(nameof(HasSequenceSteps));
            OnPropertyChanged(nameof(HasNoSequenceSteps));
        }
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
        {
            step.SortOrder = order++;
            step.HasNextStep = order < SequenceSteps.Count;
        }
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    private string SequenceSourceKey(QuickFillFieldEditorVm field)
        => field.SourceKind switch
        {
            QuickFillFieldSourceKind.WebLogin => $"web:{field.LinkedItemId}",
            QuickFillFieldSourceKind.CreditCard => $"card:{field.LinkedItemId}",
            QuickFillFieldSourceKind.ApiKeyField => $"api:{field.LinkedItemId}",
            QuickFillFieldSourceKind.Authenticator => $"auth:{field.LinkedItemId}",
            _ => "manual"
        };

    private string SequenceSourceLabel(QuickFillFieldEditorVm field)
        => field.SourceKind switch
        {
            QuickFillFieldSourceKind.WebLogin => $"{T("QuickFill.Source.WebLogin")} - {WebLoginOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            QuickFillFieldSourceKind.CreditCard => $"{T("QuickFill.Source.CreditCard")} - {CreditCardOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            QuickFillFieldSourceKind.ApiKeyField => $"{T("QuickFill.Source.ApiKey")} - {ApiKeyOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            QuickFillFieldSourceKind.Authenticator => $"{T("QuickFill.Source.Authenticator")} - {AuthenticatorOptions.FirstOrDefault(option => option.Id == field.LinkedItemId)?.Label ?? field.SourceDisplay}",
            _ => T("QuickFill.Source.Manual")
        };

    private static string SequenceFieldLabel(QuickFillFieldEditorVm field)
        => field.Label;

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
        foreach (var option in CreditCardFieldOptions)
            option.RefreshLocalization(_root);
        foreach (var option in SequenceStepKindOptions)
            option.RefreshLocalization(_root);
        foreach (var option in KeystrokeOptions)
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

    private static QuickFillCreditCardFieldOption[] CreateCreditCardFieldOptions() =>
    [
        new("cardholder", QuickFillFieldKind.Text, false, "QuickFill.Field.Cardholder"),
        new("number", QuickFillFieldKind.Secret, true, "QuickFill.Field.CardNumber"),
        new("expiry_month", QuickFillFieldKind.Text, false, "QuickFill.Field.ExpiryMonth"),
        new("expiry_year", QuickFillFieldKind.Text, false, "QuickFill.Field.ExpiryYear"),
        new("expiry", QuickFillFieldKind.Text, false, "QuickFill.Field.Expiry"),
        new("cvc", QuickFillFieldKind.Secret, true, "QuickFill.Field.Cvc"),
        new("bank", QuickFillFieldKind.Text, false, "QuickFill.Field.Bank")
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
        new(QuickFillKeystrokeKind.Enter, "QuickFill.Sequence.Keystroke.Enter"),
        new(QuickFillKeystrokeKind.Escape, "QuickFill.Sequence.Keystroke.Escape", "Escape"),
        new(QuickFillKeystrokeKind.Space, "QuickFill.Sequence.Keystroke.Space", "Space"),
        new(QuickFillKeystrokeKind.Backspace, "QuickFill.Sequence.Keystroke.Backspace", "Backspace"),
        new(QuickFillKeystrokeKind.Delete, "QuickFill.Sequence.Keystroke.Delete", "Delete"),
        new(QuickFillKeystrokeKind.ArrowLeft, "QuickFill.Sequence.Keystroke.ArrowLeft", "Left"),
        new(QuickFillKeystrokeKind.ArrowRight, "QuickFill.Sequence.Keystroke.ArrowRight", "Right"),
        new(QuickFillKeystrokeKind.ArrowUp, "QuickFill.Sequence.Keystroke.ArrowUp", "Up"),
        new(QuickFillKeystrokeKind.ArrowDown, "QuickFill.Sequence.Keystroke.ArrowDown", "Down"),
        new(QuickFillKeystrokeKind.Home, "QuickFill.Sequence.Keystroke.Home", "Home"),
        new(QuickFillKeystrokeKind.End, "QuickFill.Sequence.Keystroke.End", "End"),
        new(QuickFillKeystrokeKind.PageUp, "QuickFill.Sequence.Keystroke.PageUp", "Page Up"),
        new(QuickFillKeystrokeKind.PageDown, "QuickFill.Sequence.Keystroke.PageDown", "Page Down"),
        new(QuickFillKeystrokeKind.Insert, "QuickFill.Sequence.Keystroke.Insert", "Insert"),
        .. Enum.GetValues<QuickFillKeystrokeKind>()
            .Where(key => key is >= QuickFillKeystrokeKind.F1 and <= QuickFillKeystrokeKind.F12)
            .Select(key => new QuickFillKeystrokeOption(key, $"QuickFill.Sequence.Keystroke.{key}", key.ToString())),
        .. Enum.GetValues<QuickFillKeystrokeKind>()
            .Where(key => key is >= QuickFillKeystrokeKind.A and <= QuickFillKeystrokeKind.Z)
            .Select(key => new QuickFillKeystrokeOption(key, $"QuickFill.Sequence.Keystroke.{key}", key.ToString())),
        .. Enum.GetValues<QuickFillKeystrokeKind>()
            .Where(key => key is >= QuickFillKeystrokeKind.D0 and <= QuickFillKeystrokeKind.D9)
            .Select(key => new QuickFillKeystrokeOption(key, $"QuickFill.Sequence.Keystroke.{key}", key.ToString()[1..]))
    ];
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

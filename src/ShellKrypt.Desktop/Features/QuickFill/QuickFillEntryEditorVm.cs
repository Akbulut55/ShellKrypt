using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Application.QuickFill;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Features.QuickFill;

public sealed partial class QuickFillEntryEditorVm : ViewModelBase
{
    private readonly Func<string, object[], string> _translate;
    private IReadOnlyList<WebLoginEntry> _webLogins = [];
    private IReadOnlyList<CardEntry> _creditCards = [];
    private IReadOnlyList<ApiKeyEntry> _apiKeys = [];
    private IReadOnlyList<AuthenticatorEntry> _authenticators = [];

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
    [ObservableProperty] private QuickFillLinkedItemOption? selectedWebLoginOption;
    [ObservableProperty] private QuickFillWebLoginFieldOption? selectedWebLoginFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedCreditCardOption;
    [ObservableProperty] private QuickFillCreditCardFieldOption? selectedCreditCardFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedApiKeyFieldOption;
    [ObservableProperty] private QuickFillLinkedItemOption? selectedAuthenticatorOption;
    [ObservableProperty] private bool canDeleteEntry;

    public QuickFillEntryEditorVm(Func<string, object[], string> translate)
    {
        _translate = translate;

        foreach (var option in CreateWebLoginFieldOptions())
            WebLoginFieldOptions.Add(option);
        foreach (var option in CreateCreditCardFieldOptions())
            CreditCardFieldOptions.Add(option);
        foreach (var option in CreateKeystrokeOptions())
            KeystrokeOptions.Add(option);

        RefreshLocalization();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
        SelectedCreditCardFieldOption = CreditCardFieldOptions.FirstOrDefault();
        Reset();
    }

    public Func<QuickFillEntryInput, Task>? SaveRequested { get; set; }
    public Func<Task>? DeleteRequested { get; set; }
    public Action? CancelRequested { get; set; }

    public ObservableCollection<QuickFillFieldEditorVm> Fields { get; } = new();
    public ObservableCollection<QuickFillWebLoginFieldOption> WebLoginFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> WebLoginOptions { get; } = new();
    public ObservableCollection<QuickFillCreditCardFieldOption> CreditCardFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> CreditCardOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> ApiKeyFieldOptions { get; } = new();
    public ObservableCollection<QuickFillLinkedItemOption> AuthenticatorOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceStepEditorVm> SequenceSteps { get; } = new();
    public ObservableCollection<QuickFillKeystrokeOption> KeystrokeOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceFieldOption> SequenceFieldOptions { get; } = new();

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
    public bool HasTargetWindowTitleContains => !string.IsNullOrWhiteSpace(TargetWindowTitleContains);
    public string TargetProcessDisplay => string.IsNullOrWhiteSpace(TargetProcessName)
        ? T("QuickFill.Editor.TargetNotSet")
        : TargetProcessName;
    public string SequenceStepCountText => T("QuickFill.Sequence.ConfiguredCount", SequenceSteps.Count);

    partial void OnSelectedApiKeyOptionChanged(QuickFillLinkedItemOption? value) => RefreshApiKeyFieldOptions(value?.Id);
    partial void OnTargetProcessNameChanged(string value) => OnPropertyChanged(nameof(TargetProcessDisplay));
    partial void OnTargetWindowTitleContainsChanged(string value) => OnPropertyChanged(nameof(HasTargetWindowTitleContains));
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

    public override void RefreshLocalization()
    {
        foreach (var option in WebLoginFieldOptions)
            option.RefreshLocalization(T);
        foreach (var option in CreditCardFieldOptions)
            option.RefreshLocalization(T);
        foreach (var option in KeystrokeOptions)
            option.RefreshLocalization(T);

        RefreshSequenceFieldOptions();
        NotifyLocalized(nameof(TargetProcessDisplay));
        NotifyLocalized(nameof(SequenceStepCountText));
        NotifyLocalized(nameof(PendingKeyPreviewText));
    }

    public void SetLinkedSources(
        IReadOnlyList<WebLoginEntry> webLogins,
        IReadOnlyList<CardEntry> creditCards,
        IReadOnlyList<ApiKeyEntry> apiKeys,
        IReadOnlyList<AuthenticatorEntry> authenticators)
    {
        _webLogins = webLogins;
        _creditCards = creditCards;
        _apiKeys = apiKeys;
        _authenticators = authenticators;
        RefreshLinkedOptions();
        RefreshSequenceFieldOptions();
    }

    public void Reset()
    {
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
        NotifyEditorCollectionsChanged();
    }

    public void PrepareFromTarget(QuickFillTargetContext target)
    {
        Reset();
        TargetProcessName = target.ProcessName;
        TargetWindowTitleContains = target.WindowTitle;
        EntryName = string.IsNullOrWhiteSpace(target.ProcessName)
            ? T("QuickFill.Editor.NewEntry")
            : target.ProcessName;
    }

    public void Populate(QuickFillEntry? entry)
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
        NotifyEditorCollectionsChanged();
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    public QuickFillEntryInput BuildInput()
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

    [RelayCommand] private void SelectAddFieldMode() => AddStepMode = QuickFillAddStepMode.Field;
    [RelayCommand] private void SelectAddKeyMode() => AddStepMode = QuickFillAddStepMode.Key;
    [RelayCommand] private void SelectAddTextMode() => AddStepMode = QuickFillAddStepMode.Text;
    [RelayCommand] private void SelectAddDelayMode() => AddStepMode = QuickFillAddStepMode.Delay;
    [RelayCommand] private void SelectManualFieldSource() => AddFieldSource = QuickFillAddFieldSource.Manual;
    [RelayCommand] private void SelectWebLoginFieldSource() => AddFieldSource = QuickFillAddFieldSource.WebLogin;
    [RelayCommand] private void SelectCreditCardFieldSource() => AddFieldSource = QuickFillAddFieldSource.CreditCard;
    [RelayCommand] private void SelectApiKeyFieldSource() => AddFieldSource = QuickFillAddFieldSource.ApiKey;
    [RelayCommand] private void SelectAuthenticatorFieldSource() => AddFieldSource = QuickFillAddFieldSource.Authenticator;

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
        NotifyEditorCollectionsChanged();
    }

    public void AddCapturedKeyStep(QuickFillKeystrokeKind key, QuickFillKeyModifiers modifiers)
    {
        PendingKeyStep = key;
        PendingKeyModifiers = modifiers;
        HasPendingKeyStep = true;
    }

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
        if (SaveRequested is not null)
            await SaveRequested(BuildInput());
    }

    [RelayCommand]
    private async Task DeleteEntryAsync()
    {
        if (DeleteRequested is not null)
            await DeleteRequested();
    }

    [RelayCommand]
    private void CancelEdit() => CancelRequested?.Invoke();

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
        SequenceSteps.Add(QuickFillSequenceStepEditorVm.FromStep(step, KeystrokeOptions, SequenceFieldOptions));
        ResequenceSequenceSteps();
        if (notify)
        {
            OnPropertyChanged(nameof(HasSequenceSteps));
            OnPropertyChanged(nameof(HasNoSequenceSteps));
        }
        OnPropertyChanged(nameof(SequenceStepCountText));
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

        ResetAddFieldInputs();
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
        SelectedWebLoginOption = WebLoginOptions.FirstOrDefault();
        SelectedWebLoginFieldOption = WebLoginFieldOptions.FirstOrDefault();
        SelectedCreditCardOption = CreditCardOptions.FirstOrDefault();
        SelectedCreditCardFieldOption = CreditCardFieldOptions.FirstOrDefault();
        SelectedApiKeyOption = ApiKeyOptions.FirstOrDefault();
        SelectedApiKeyFieldOption = ApiKeyFieldOptions.FirstOrDefault();
        SelectedAuthenticatorOption = AuthenticatorOptions.FirstOrDefault();
    }

    private void RefreshSequenceFieldOptions()
    {
        SequenceFieldOptions.Clear();
        foreach (var field in Fields.OrderBy(field => field.SortOrder))
            SequenceFieldOptions.Add(new QuickFillSequenceFieldOption(field.Id, field.Label, SequenceSourceKey(field), SequenceSourceLabel(field), field.IsSensitive));

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

    private void NotifyEditorCollectionsChanged()
    {
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasSequenceSteps));
        OnPropertyChanged(nameof(HasNoSequenceSteps));
        OnPropertyChanged(nameof(SequenceStepCountText));
    }

    private string T(string key, params object[] args) => _translate(key, args);

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

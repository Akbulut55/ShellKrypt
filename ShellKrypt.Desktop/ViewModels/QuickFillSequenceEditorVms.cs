using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class QuickFillSequenceStepEditorVm : ObservableObject
{
    [ObservableProperty] private string id = "";
    [ObservableProperty] private QuickFillSequenceStepKind kind = QuickFillSequenceStepKind.Field;
    [ObservableProperty] private int sortOrder;
    [ObservableProperty] private string fieldId = "";
    [ObservableProperty] private QuickFillKeystrokeKind keystroke = QuickFillKeystrokeKind.Tab;
    [ObservableProperty] private string text = "";
    [ObservableProperty] private int delayMilliseconds = 250;
    [ObservableProperty] private bool ctrlModifier;
    [ObservableProperty] private bool altModifier;
    [ObservableProperty] private bool shiftModifier;
    [ObservableProperty] private bool metaModifier;
    [ObservableProperty] private int repeatCount = 1;
    [ObservableProperty] private QuickFillSequenceStepKindOption? selectedKindOption;
    [ObservableProperty] private QuickFillSequenceSourceOption? selectedSourceOption;
    [ObservableProperty] private QuickFillSequenceFieldOption? selectedFieldOption;
    [ObservableProperty] private QuickFillKeystrokeOption? selectedKeystrokeOption;

    public ObservableCollection<QuickFillSequenceStepKindOption> KindOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceSourceOption> SourceOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceFieldOption> FieldOptions { get; } = new();
    public ObservableCollection<QuickFillSequenceFieldOption> FilteredFieldOptions { get; } = new();
    public ObservableCollection<QuickFillKeystrokeOption> KeystrokeOptions { get; } = new();

    public int StepNumber => SortOrder + 1;
    public int DisplayNumber => StepNumber;
    public bool IsFieldStep => Kind == QuickFillSequenceStepKind.Field;
    public bool IsKeystrokeStep => Kind == QuickFillSequenceStepKind.Keystroke;
    public bool IsTextStep => Kind == QuickFillSequenceStepKind.LiteralText;
    public bool IsDelayStep => Kind == QuickFillSequenceStepKind.Delay;
    public bool ShowSourceSelector => IsFieldStep;
    public bool ShowFieldSelector => IsFieldStep;
    public bool ShowValueSelector => IsKeystrokeStep;
    public bool ShowTextInput => IsTextStep;
    public bool ShowDelayInput => IsDelayStep;

    public static QuickFillSequenceStepEditorVm FromStep(
        QuickFillSequenceStep step,
        IEnumerable<QuickFillSequenceStepKindOption> kindOptions,
        IEnumerable<QuickFillKeystrokeOption> keystrokeOptions,
        IEnumerable<QuickFillSequenceFieldOption> fieldOptions)
    {
        var vm = new QuickFillSequenceStepEditorVm
        {
            Id = step.Id,
            Kind = step.Kind,
            SortOrder = step.SortOrder,
            FieldId = step.FieldId,
            Keystroke = step.Keystroke,
            Text = step.Text,
            DelayMilliseconds = step.DelayMilliseconds <= 0 ? 250 : step.DelayMilliseconds,
            CtrlModifier = step.Modifiers.HasFlag(QuickFillKeyModifiers.Ctrl),
            AltModifier = step.Modifiers.HasFlag(QuickFillKeyModifiers.Alt),
            ShiftModifier = step.Modifiers.HasFlag(QuickFillKeyModifiers.Shift),
            MetaModifier = step.Modifiers.HasFlag(QuickFillKeyModifiers.Meta),
            RepeatCount = step.RepeatCount <= 0 ? 1 : step.RepeatCount
        };
        vm.ReplaceKindOptions(kindOptions);
        vm.ReplaceKeystrokeOptions(keystrokeOptions);
        vm.ReplaceFieldOptions(fieldOptions);
        vm.SelectedKindOption = vm.KindOptions.FirstOrDefault(option => option.Kind == vm.Kind) ?? vm.KindOptions.FirstOrDefault();
        vm.SelectedKeystrokeOption = vm.KeystrokeOptions.FirstOrDefault(option => option.Keystroke == vm.Keystroke) ?? vm.KeystrokeOptions.FirstOrDefault();
        vm.SelectedFieldOption = vm.FieldOptions.FirstOrDefault(option => option.Id == vm.FieldId) ?? vm.FieldOptions.FirstOrDefault();
        return vm;
    }

    public QuickFillSequenceStep ToStep()
        => new(
            string.IsNullOrWhiteSpace(Id) ? System.Guid.NewGuid().ToString("N") : Id,
            Kind,
            SortOrder,
            FieldId,
            Keystroke,
            Text,
            DelayMilliseconds,
            BuildModifiers(),
            RepeatCount <= 0 ? 1 : RepeatCount);

    public void ReplaceKindOptions(IEnumerable<QuickFillSequenceStepKindOption> options)
    {
        var selected = Kind;
        KindOptions.Clear();
        foreach (var option in options)
            KindOptions.Add(option);
        SelectedKindOption = KindOptions.FirstOrDefault(option => option.Kind == selected) ?? KindOptions.FirstOrDefault();
    }

    public void ReplaceKeystrokeOptions(IEnumerable<QuickFillKeystrokeOption> options)
    {
        var selected = Keystroke;
        KeystrokeOptions.Clear();
        foreach (var option in options)
            KeystrokeOptions.Add(option);
        SelectedKeystrokeOption = KeystrokeOptions.FirstOrDefault(option => option.Keystroke == selected) ?? KeystrokeOptions.FirstOrDefault();
    }

    public void ReplaceFieldOptions(IEnumerable<QuickFillSequenceFieldOption> options)
    {
        var selected = FieldId;
        FieldOptions.Clear();
        foreach (var option in options)
            FieldOptions.Add(option);
        RefreshSourceOptions(selected);
        RefreshFilteredFieldOptions(selected);
    }

    private void RefreshSourceOptions(string selectedFieldId)
    {
        var selectedSource = FieldOptions.FirstOrDefault(option => option.Id == selectedFieldId)?.SourceKey
            ?? SelectedSourceOption?.Key
            ?? "";

        SourceOptions.Clear();
        foreach (var source in FieldOptions
                     .GroupBy(option => option.SourceKey)
                     .Select(group => group.First())
                     .OrderBy(option => option.SourceLabel))
        {
            SourceOptions.Add(new QuickFillSequenceSourceOption(source.SourceKey, source.SourceLabel));
        }

        SelectedSourceOption = SourceOptions.FirstOrDefault(option => option.Key == selectedSource)
            ?? SourceOptions.FirstOrDefault();
    }

    private void RefreshFilteredFieldOptions(string selectedFieldId)
    {
        var selectedSource = SelectedSourceOption?.Key ?? "";
        FilteredFieldOptions.Clear();
        foreach (var option in FieldOptions.Where(option => string.IsNullOrWhiteSpace(selectedSource) || option.SourceKey == selectedSource))
            FilteredFieldOptions.Add(option);

        SelectedFieldOption = FilteredFieldOptions.FirstOrDefault(option => option.Id == selectedFieldId)
            ?? FilteredFieldOptions.FirstOrDefault();
    }

    partial void OnSortOrderChanged(int value)
    {
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(DisplayNumber));
    }

    partial void OnSelectedKindOptionChanged(QuickFillSequenceStepKindOption? value)
    {
        if (value is not null)
            Kind = value.Kind;
    }

    partial void OnKindChanged(QuickFillSequenceStepKind value)
    {
        OnPropertyChanged(nameof(IsFieldStep));
        OnPropertyChanged(nameof(IsKeystrokeStep));
        OnPropertyChanged(nameof(IsTextStep));
        OnPropertyChanged(nameof(IsDelayStep));
        OnPropertyChanged(nameof(ShowSourceSelector));
        OnPropertyChanged(nameof(ShowFieldSelector));
        OnPropertyChanged(nameof(ShowValueSelector));
        OnPropertyChanged(nameof(ShowTextInput));
        OnPropertyChanged(nameof(ShowDelayInput));
    }

    partial void OnSelectedSourceOptionChanged(QuickFillSequenceSourceOption? value)
    {
        RefreshFilteredFieldOptions(FieldId);
    }

    partial void OnSelectedFieldOptionChanged(QuickFillSequenceFieldOption? value)
    {
        FieldId = value?.Id ?? "";
    }

    partial void OnSelectedKeystrokeOptionChanged(QuickFillKeystrokeOption? value)
    {
        if (value is not null)
            Keystroke = value.Keystroke;
    }

    private QuickFillKeyModifiers BuildModifiers()
    {
        var modifiers = QuickFillKeyModifiers.None;
        if (CtrlModifier)
            modifiers |= QuickFillKeyModifiers.Ctrl;
        if (AltModifier)
            modifiers |= QuickFillKeyModifiers.Alt;
        if (ShiftModifier)
            modifiers |= QuickFillKeyModifiers.Shift;
        if (MetaModifier)
            modifiers |= QuickFillKeyModifiers.Meta;
        return modifiers;
    }
}

public sealed class QuickFillSequenceSourceOption
{
    public QuickFillSequenceSourceOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    public override string ToString() => Label;
}

public sealed partial class QuickFillSequenceStepKindOption : ObservableObject
{
    private readonly string _labelKey;

    public QuickFillSequenceStepKindOption(QuickFillSequenceStepKind kind, string labelKey)
    {
        Kind = kind;
        _labelKey = labelKey;
        Label = labelKey;
    }

    public QuickFillSequenceStepKind Kind { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(MainWindowViewModel root) => Label = root.Localization.Get(_labelKey);
    public override string ToString() => Label;
}

public sealed partial class QuickFillKeystrokeOption : ObservableObject
{
    private readonly string _labelKey;
    private readonly string _fallbackLabel;

    public QuickFillKeystrokeOption(QuickFillKeystrokeKind keystroke, string labelKey, string? fallbackLabel = null)
    {
        Keystroke = keystroke;
        _labelKey = labelKey;
        _fallbackLabel = fallbackLabel ?? labelKey;
        Label = labelKey;
    }

    public QuickFillKeystrokeKind Keystroke { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(MainWindowViewModel root)
    {
        var localized = root.Localization.Get(_labelKey);
        Label = string.Equals(localized, _labelKey, System.StringComparison.Ordinal) ? _fallbackLabel : localized;
    }
    public override string ToString() => Label;
}

public sealed class QuickFillSequenceFieldOption
{
    public QuickFillSequenceFieldOption(string id, string label, string sourceKey = "", string sourceLabel = "", bool isSensitive = false)
    {
        Id = id;
        Label = label;
        SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? "manual" : sourceKey;
        SourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "Manual" : sourceLabel;
        IsSensitive = isSensitive;
    }

    public string Id { get; }
    public string Label { get; }
    public string SourceKey { get; }
    public string SourceLabel { get; }
    public bool IsSensitive { get; }

    public override string ToString() => Label;
}

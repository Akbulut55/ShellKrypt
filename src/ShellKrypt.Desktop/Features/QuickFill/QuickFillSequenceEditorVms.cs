using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Features.QuickFill;

public enum QuickFillAddStepMode
{
    Field,
    Key,
    Text,
    Delay
}

public enum QuickFillAddFieldSource
{
    Manual,
    WebLogin,
    CreditCard,
    ApiKey,
    Authenticator
}

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
    [ObservableProperty] private QuickFillSequenceFieldOption? selectedFieldOption;
    [ObservableProperty] private QuickFillKeystrokeOption? selectedKeystrokeOption;
    [ObservableProperty] private bool hasNextStep;

    public ObservableCollection<QuickFillSequenceFieldOption> FieldOptions { get; } = new();
    public ObservableCollection<QuickFillKeystrokeOption> KeystrokeOptions { get; } = new();

    public int StepNumber => SortOrder + 1;
    public int DisplayNumber => StepNumber;
    public bool IsFieldStep => Kind == QuickFillSequenceStepKind.Field;
    public bool IsKeystrokeStep => Kind == QuickFillSequenceStepKind.Keystroke;
    public bool IsTextStep => Kind == QuickFillSequenceStepKind.LiteralText;
    public bool IsDelayStep => Kind == QuickFillSequenceStepKind.Delay;
    public bool IsSensitiveField => IsFieldStep && SelectedFieldOption?.IsSensitive == true;
    public bool IsLinkedField => IsFieldStep && SelectedFieldOption is not null && SelectedFieldOption.SourceKey != "manual";
    public bool IsKeyStep => IsKeystrokeStep;
    public string DisplayKind => Kind switch
    {
        QuickFillSequenceStepKind.Field => "Field",
        QuickFillSequenceStepKind.Keystroke => "Key",
        QuickFillSequenceStepKind.LiteralText => "Text",
        QuickFillSequenceStepKind.Delay => "Delay",
        _ => "Step"
    };
    public string DisplayLabel => Kind switch
    {
        QuickFillSequenceStepKind.Field => SelectedFieldOption?.Label ?? "Field",
        QuickFillSequenceStepKind.Keystroke => KeyDisplay,
        QuickFillSequenceStepKind.LiteralText => string.IsNullOrWhiteSpace(Text) ? "\"text\"" : $"\"{TrimText(Text)}\"",
        QuickFillSequenceStepKind.Delay => $"Delay {DelayMilliseconds} ms",
        _ => "Step"
    };
    public string DisplaySubLabel => Kind switch
    {
        QuickFillSequenceStepKind.Field => SelectedFieldOption?.SourceLabel ?? "Manual field",
        QuickFillSequenceStepKind.Keystroke => "Key",
        QuickFillSequenceStepKind.LiteralText => "Text",
        QuickFillSequenceStepKind.Delay => "Delay",
        _ => ""
    };
    private string KeyDisplay
    {
        get
        {
            return QuickFillKeyDisplayFormatter.Format(Keystroke, BuildModifiers(), RepeatCount, SelectedKeystrokeOption?.Label);
        }
    }

    public static QuickFillSequenceStepEditorVm FromStep(
        QuickFillSequenceStep step,
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
        vm.ReplaceKeystrokeOptions(keystrokeOptions);
        vm.ReplaceFieldOptions(fieldOptions);
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
        SelectedFieldOption = FieldOptions.FirstOrDefault(option => option.Id == selected)
            ?? FieldOptions.FirstOrDefault();
    }

    partial void OnSortOrderChanged(int value)
    {
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(DisplayNumber));
    }

    partial void OnKindChanged(QuickFillSequenceStepKind value)
    {
        OnPropertyChanged(nameof(IsFieldStep));
        OnPropertyChanged(nameof(IsKeystrokeStep));
        OnPropertyChanged(nameof(IsTextStep));
        OnPropertyChanged(nameof(IsDelayStep));
        NotifyDisplayChanged();
    }

    partial void OnSelectedFieldOptionChanged(QuickFillSequenceFieldOption? value)
    {
        FieldId = value?.Id ?? "";
        NotifyDisplayChanged();
    }

    partial void OnSelectedKeystrokeOptionChanged(QuickFillKeystrokeOption? value)
    {
        if (value is not null)
            Keystroke = value.Keystroke;
    }

    partial void OnKeystrokeChanged(QuickFillKeystrokeKind value) => NotifyDisplayChanged();
    partial void OnTextChanged(string value) => NotifyDisplayChanged();
    partial void OnDelayMillisecondsChanged(int value) => NotifyDisplayChanged();
    partial void OnCtrlModifierChanged(bool value) => NotifyDisplayChanged();
    partial void OnAltModifierChanged(bool value) => NotifyDisplayChanged();
    partial void OnShiftModifierChanged(bool value) => NotifyDisplayChanged();
    partial void OnMetaModifierChanged(bool value) => NotifyDisplayChanged();
    partial void OnRepeatCountChanged(int value) => NotifyDisplayChanged();

    private void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(IsSensitiveField));
        OnPropertyChanged(nameof(IsLinkedField));
        OnPropertyChanged(nameof(IsKeyStep));
        OnPropertyChanged(nameof(DisplayKind));
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(DisplaySubLabel));
    }

    private static string TrimText(string value)
        => value.Length <= 24 ? value : value[..24] + "...";

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

public static class QuickFillKeyDisplayFormatter
{
    public static string Format(
        QuickFillKeystrokeKind key,
        QuickFillKeyModifiers modifiers,
        int repeatCount = 1,
        string? keyLabel = null)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(QuickFillKeyModifiers.Ctrl))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(QuickFillKeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(QuickFillKeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(QuickFillKeyModifiers.Meta))
            parts.Add("Meta");

        parts.Add(string.IsNullOrWhiteSpace(keyLabel) ? DisplayKeyName(key) : keyLabel);
        var value = string.Join("+", parts);
        return repeatCount > 1 ? $"{value} x{repeatCount}" : value;
    }

    private static string DisplayKeyName(QuickFillKeystrokeKind key)
        => key switch
        {
            QuickFillKeystrokeKind.ArrowLeft => "Left",
            QuickFillKeystrokeKind.ArrowRight => "Right",
            QuickFillKeystrokeKind.ArrowUp => "Up",
            QuickFillKeystrokeKind.ArrowDown => "Down",
            QuickFillKeystrokeKind.PageUp => "Page Up",
            QuickFillKeystrokeKind.PageDown => "Page Down",
            >= QuickFillKeystrokeKind.D0 and <= QuickFillKeystrokeKind.D9 => ((int)(key - QuickFillKeystrokeKind.D0)).ToString(),
            _ => key.ToString()
        };
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

    public void RefreshLocalization(Func<string, object[], string> translate)
    {
        var localized = translate(_labelKey, []);
        Label = string.Equals(localized, _labelKey, System.StringComparison.Ordinal) ? _fallbackLabel : localized;
    }
    public override string ToString() => Label;
}

public sealed partial class QuickFillCreditCardFieldOption : ObservableObject
{
    private readonly string _labelKey;

    public QuickFillCreditCardFieldOption(string fieldName, QuickFillFieldKind kind, bool isSensitive, string labelKey)
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

    public void RefreshLocalization(Func<string, object[], string> translate) => Label = translate(_labelKey, []);
    public override string ToString() => Label;
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
        QuickFillFieldSourceKind.CreditCard => "Credit Card",
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

    public void RefreshLocalization(Func<string, object[], string> translate) => Label = translate(_labelKey, []);
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

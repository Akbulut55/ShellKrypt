using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ProjectSecretRowVm : ObservableObject
{
    public ProjectSecretRowVm(ProjectSecretEntry entry)
    {
        Entry = entry;
    }

    public ProjectSecretEntry Entry { get; private set; }
    public string Id => Entry.Id;
    public string Name => Entry.Name;
    public string Description => Entry.Description;
    public string ProjectRootPath => Entry.ProjectRootPath ?? "";
    public int EnvironmentCount => Entry.Environments.Count;
    public int VariableCount => Entry.Environments.Sum(environment => environment.Variables.Count);
    public int WarningCount => ProjectSecretAuditBuilder.BuildFindings(Entry).Count;
    public string Summary => $"{EnvironmentCount} env / {VariableCount} variables";
    public string WarningSummary => WarningCount == 0 ? "No warnings" : $"{WarningCount} warning(s)";
    public string RootDisplay => string.IsNullOrWhiteSpace(ProjectRootPath) ? "No project root" : Path.GetFileName(ProjectRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public bool HasWarnings => WarningCount > 0;

    public void Update(ProjectSecretEntry entry)
    {
        Entry = entry;
        OnPropertyChanged(string.Empty);
    }
}

public sealed partial class ProjectSecretVariableRowVm : ObservableObject
{
    public ProjectSecretVariableRowVm(ProjectSecretVariableEntry entry)
    {
        Entry = entry;
        IsValueRevealed = false;
    }

    public ProjectSecretVariableEntry Entry { get; private set; }
    public string Id => Entry.Id;
    public string Key => Entry.Key;
    public string Value => Entry.Value;
    public bool IsSecret => Entry.IsSecret;
    public string Notes => Entry.Notes;
    public ProjectSecretVariableSourceKind SourceKind => Entry.SourceKind;
    public string SourceLabel => ProjectSecretDisplayFormatter.SourceLabel(Entry.SourceKind);
    public string ValueDisplay => Entry.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey
        ? "Referenced API Key"
        : IsValueRevealed || !Entry.IsSecret ? Entry.Value : ProjectSecretDisplayFormatter.MaskValue(Entry.Value);
    public string NotesDisplay => string.IsNullOrWhiteSpace(Entry.Notes) ? "-" : Entry.Notes.Trim();
    public string LinkDisplay => Entry.SourceKind switch
    {
        ProjectSecretVariableSourceKind.LinkedApiKey => string.IsNullOrWhiteSpace(Entry.LinkedFieldName) ? "Referenced API Key" : $"Reference: {Entry.LinkedFieldName}",
        ProjectSecretVariableSourceKind.ImportedApiKey => "Imported API Key",
        ProjectSecretVariableSourceKind.ImportedEnvFile => "Imported .env",
        _ => ""
    };
    public bool IsLinked => Entry.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey;
    public bool IsManual => Entry.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey;
    public bool CanRevealEditValue => IsManual && EditIsSecret;
    public bool UseMaskedEditValueInput => IsEditing && CanRevealEditValue && !IsValueRevealed;
    public bool UsePlainEditValueInput => IsEditing && (!CanRevealEditValue || IsValueRevealed);
    public bool ShowEditAction => OwnerIsProjectEditing && !IsEditing;
    public bool ShowDeleteAction => OwnerIsProjectEditing;

    [ObservableProperty] private bool isValueRevealed;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool ownerIsProjectEditing;
    [ObservableProperty] private string editKey = "";
    [ObservableProperty] private string editValue = "";
    [ObservableProperty] private string editNotes = "";
    [ObservableProperty] private bool editIsSecret;

    partial void OnIsValueRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueDisplay));
        OnPropertyChanged(nameof(UseMaskedEditValueInput));
        OnPropertyChanged(nameof(UsePlainEditValueInput));
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value)
            IsValueRevealed = false;

        OnPropertyChanged(nameof(UseMaskedEditValueInput));
        OnPropertyChanged(nameof(UsePlainEditValueInput));
        OnPropertyChanged(nameof(ShowEditAction));
    }

    partial void OnEditIsSecretChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRevealEditValue));
        OnPropertyChanged(nameof(UseMaskedEditValueInput));
        OnPropertyChanged(nameof(UsePlainEditValueInput));
    }

    partial void OnOwnerIsProjectEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEditAction));
        OnPropertyChanged(nameof(ShowDeleteAction));
    }

    public void Update(ProjectSecretVariableEntry entry)
    {
        Entry = entry;
        ResetEditState();
        OnPropertyChanged(string.Empty);
    }

    public void BeginEdit()
    {
        ResetEditState();
        IsEditing = true;
    }

    public void CancelEdit()
    {
        ResetEditState();
        IsEditing = false;
    }

    public ProjectSecretVariableEntry BuildEditedEntry()
        => Entry with
        {
            Key = EditKey.Trim(),
            Value = Entry.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? "" : EditValue,
            Notes = EditNotes.Trim(),
            IsSecret = EditIsSecret,
            LastUpdatedAtUtc = System.DateTimeOffset.UtcNow.ToString("O")
        };

    private void ResetEditState()
    {
        EditKey = Entry.Key;
        EditValue = Entry.Value;
        EditNotes = Entry.Notes;
        EditIsSecret = Entry.IsSecret;
    }
}

public sealed record ProjectSecretEnvironmentOption(string Id, string Name, ProjectSecretEnvironmentKind Kind, string ProfileName = "")
{
    public string StageLabel => string.IsNullOrWhiteSpace(ProfileName) ? Kind.ToString() : ProfileName.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(Name) || string.Equals(Name, StageLabel, StringComparison.OrdinalIgnoreCase)
        ? StageLabel
        : $"{Name} / {StageLabel}";
}

public sealed partial class ProjectSecretProfileSelectionVm : ObservableObject
{
    public ProjectSecretProfileSelectionVm(string name)
    {
        Name = name.Trim();
    }

    public string Name { get; }
}

public sealed record ProjectSecretEnvironmentNameOption(string Name)
{
    public string DisplayName => Name;
}

public sealed partial class ProjectSecretNameChipVm : ObservableObject
{
    public ProjectSecretNameChipVm(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public string BackgroundResourceKey => IsSelected ? "AccentBrush" : "SurfaceRaisedBrush";
    public string ForegroundResourceKey => IsSelected ? "AccentForegroundBrush" : "TextPrimaryBrush";
    public string SelectionBorderResourceKey => IsSelected ? "AccentBrush" : "BorderBrushSoft";
    public int SelectionBorderThickness => IsSelected ? 2 : 1;

    [ObservableProperty] private bool isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundResourceKey));
        OnPropertyChanged(nameof(ForegroundResourceKey));
        OnPropertyChanged(nameof(SelectionBorderResourceKey));
        OnPropertyChanged(nameof(SelectionBorderThickness));
    }
}

public sealed record ProjectSecretCompareRowVm(string Key, IReadOnlyList<ProjectSecretCompareCell> Cells);

public sealed record ProjectSecretApiKeyOption(string ItemId, string Name);

public sealed record ProjectSecretApiKeyFieldOption(string FieldId, string Label, string Value, bool IsSensitive);

public sealed record ProjectSecretApiKeyLinkOption(
    string ApiKeyItemId,
    string ApiKeyName,
    string FieldId,
    string FieldLabel,
    bool IsSensitive)
{
    public string DisplayName => $"{ApiKeyName} / {FieldLabel}";
    public string VariableKey => FieldLabel;
}

public sealed record ProjectSecretScanFindingVm(ProjectSecretScanFinding Finding)
{
    public string Kind => Finding.Kind.ToString();
    public string Severity => Finding.Severity.ToString();
    public string VariableKey => Finding.VariableKey ?? "";
    public string Location => string.IsNullOrWhiteSpace(Finding.RelativeFilePath)
        ? ""
        : Finding.LineNumber is null
            ? Finding.RelativeFilePath!
            : $"{Finding.RelativeFilePath}:{Finding.LineNumber}";
    public string Message => Finding.Message;
}

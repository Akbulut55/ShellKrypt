using System.Collections.Generic;
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
    public string ValueDisplay => IsValueRevealed || !Entry.IsSecret ? Entry.Value : ProjectSecretDisplayFormatter.MaskValue(Entry.Value);
    public string LinkDisplay => Entry.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey
        ? string.IsNullOrWhiteSpace(Entry.LinkedFieldName) ? "Linked API Key" : Entry.LinkedFieldName
        : "";

    [ObservableProperty] private bool isValueRevealed;

    partial void OnIsValueRevealedChanged(bool value) => OnPropertyChanged(nameof(ValueDisplay));

    public void Update(ProjectSecretVariableEntry entry)
    {
        Entry = entry;
        OnPropertyChanged(string.Empty);
    }
}

public sealed record ProjectSecretEnvironmentOption(string Id, string Name);

public sealed record ProjectSecretCompareRowVm(string Key, IReadOnlyList<ProjectSecretCompareCell> Cells);

public sealed record ProjectSecretApiKeyOption(string ItemId, string Name);

public sealed record ProjectSecretApiKeyFieldOption(string FieldId, string Label, string Value, bool IsSensitive);

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

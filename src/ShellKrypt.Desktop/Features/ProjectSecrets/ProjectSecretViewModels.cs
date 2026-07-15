using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.ProjectSecrets;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public sealed partial class ProjectSecretProjectVm(ProjectSecretEntry entry) : ObservableObject
{
    public ProjectSecretEntry Entry { get; private set; } = entry;
    public string Id => Entry.Id;
    public string Name => Entry.Name;
    public string Description => Entry.Description;
    public string RootDisplay => string.IsNullOrWhiteSpace(Entry.ProjectRootPath) ? "No project root" : Path.GetFileName(Entry.ProjectRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public int EnvironmentCount => Entry.Environments.Count;
    public int ProfileCount => Entry.Environments.Sum(environment => environment.Profiles.Count);
    public int VariableCount => Entry.Environments.Sum(environment => environment.Profiles.Sum(profile => profile.Variables.Count));
    public int WarningCount => ProjectSecretAuditBuilder.BuildFindings(Entry).Count;
    public string Summary => $"{EnvironmentCount} environments / {VariableCount} variables";
    public void Update(ProjectSecretEntry value) { Entry = value; OnPropertyChanged(string.Empty); }
}

public sealed partial class ProjectSecretVariableVm(ProjectSecretVariableEntry entry, Func<ProjectSecretVariableEntry, string?> resolver) : ObservableObject
{
    public ProjectSecretVariableEntry Entry { get; private set; } = entry;
    public string Id => Entry.Id;
    public string Key => Entry.Key;
    public string Notes => string.IsNullOrWhiteSpace(Entry.Notes) ? "-" : Entry.Notes;
    public string Source => ProjectSecretDisplayFormatter.SourceLabel(Entry.SourceKind);
    public bool IsReferenced => Entry.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey;
    public string ValueDisplay
    {
        get
        {
            var value = resolver(Entry) ?? "";
            return IsValueRevealed || !Entry.IsSecret ? value : ProjectSecretDisplayFormatter.MaskValue(value);
        }
    }
    [ObservableProperty] private bool isValueRevealed;
    partial void OnIsValueRevealedChanged(bool value) => OnPropertyChanged(nameof(ValueDisplay));
    public void ResetReveal() => IsValueRevealed = false;
}

public sealed partial class ProjectSecretEnvironmentOption(string id, string name) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] private bool isSelected;
}

public sealed partial class ProjectSecretProfileOption(string environmentId, string id, string name) : ObservableObject
{
    public string EnvironmentId { get; } = environmentId;
    public string Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] private bool isSelected;
}
public sealed record ProjectSecretApiKeyFieldOption(string ItemId, string ApiKeyName, string FieldId, string FieldName, string Value, bool IsSensitive)
{
    public string DisplayName => $"{ApiKeyName} / {FieldName}";
}
public sealed record ProjectSecretCompareRowVm(string Key, IReadOnlyList<ProjectSecretCompareCell> Cells);
public sealed record ProjectSecretScanFindingVm(ProjectSecretScanFinding Finding)
{
    public string Kind => Finding.Kind.ToString();
    public string Severity => Finding.Severity.ToString();
    public string VariableKey => Finding.VariableKey ?? "";
    public string Location => string.IsNullOrWhiteSpace(Finding.RelativeFilePath) ? "" : Finding.LineNumber is null ? Finding.RelativeFilePath! : $"{Finding.RelativeFilePath}:{Finding.LineNumber}";
    public string Message => Finding.Message;
}

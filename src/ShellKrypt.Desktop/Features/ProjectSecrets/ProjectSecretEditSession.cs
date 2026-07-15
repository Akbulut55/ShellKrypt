namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public sealed class ProjectSecretEditSession
{
    public ProjectSecretEntry? Original { get; private set; }
    public ProjectSecretInput Draft { get; private set; } = Empty();

    public bool IsNew => Original is null;

    public void Begin(ProjectSecretEntry entry)
    {
        Original = entry;
        Draft = ToInput(entry);
    }

    public void BeginNew()
    {
        Original = null;
        Draft = Empty();
    }

    public void Replace(ProjectSecretInput draft) => Draft = draft;

    public void Restore()
    {
        Draft = Original is null ? Empty() : ToInput(Original);
    }

    public static ProjectSecretInput ToInput(ProjectSecretEntry entry)
        => new(entry.Name, entry.Description, entry.Notes, entry.ProjectRootPath,
            (entry.Environments ?? Array.Empty<ProjectSecretEnvironmentEntry>()).Select(environment => new ProjectSecretEnvironmentInput(
                environment.Id, environment.Name, environment.Notes, environment.SortOrder,
                (environment.Profiles ?? Array.Empty<ProjectSecretProfileEntry>()).Select(profile => new ProjectSecretProfileInput(
                    profile.Id, profile.Name, profile.SortOrder,
                    (profile.Variables ?? Array.Empty<ProjectSecretVariableEntry>()).Select(variable => new ProjectSecretVariableInput(
                        variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes,
                        variable.SortOrder, variable.SourceKind, variable.ReferencedItemId,
                        variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc)).ToArray())).ToArray())).ToArray(),
            (entry.ScanResults ?? Array.Empty<ProjectSecretScanResult>()).ToArray());

    public static ProjectSecretInput Empty()
        => new("", "", "", null, Array.Empty<ProjectSecretEnvironmentInput>(), Array.Empty<ProjectSecretScanResult>());
}

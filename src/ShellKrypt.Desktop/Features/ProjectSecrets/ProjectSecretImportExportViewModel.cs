using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretImportExportViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _root;
    private readonly IProjectSecretEnvParser _parser;
    private readonly IProjectSecretEnvWriter _writer;
    private readonly IProjectSecretValueResolver _resolver;
    private readonly Func<IReadOnlyList<ApiKeyEntry>> _apiKeys;
    private ProjectSecretInput _project = ProjectSecretEditSession.Empty();
    private ProjectSecretEnvParseResult? _parseResult;
    public Func<ProjectSecretInput, Task>? PersistRequested { get; set; }

    public ObservableCollection<ProjectSecretEnvironmentOption> Environments { get; } = [];
    public ObservableCollection<ProjectSecretProfileOption> Profiles { get; } = [];
    public ObservableCollection<ProjectSecretEnvImportPreviewRow> PreviewRows { get; } = [];

    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private ProjectSecretProfileOption? selectedProfile;
    [ObservableProperty] private string importPath = "";
    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private ProjectSecretEnvImportConflictStrategy conflictStrategy = ProjectSecretEnvImportConflictStrategy.ReplaceExisting;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string error = "";

    public bool HasTarget => SelectedProfile is not null;
    public bool HasPreview => PreviewRows.Count > 0;
    public bool CanOperate => IsEnabled && HasTarget && !IsBusy;
    public bool ShowUnavailable => !IsEnabled;
    public bool CanImport => CanOperate && HasPreview;
    public bool ReplaceExisting => ConflictStrategy == ProjectSecretEnvImportConflictStrategy.ReplaceExisting;
    public bool SkipExisting => ConflictStrategy == ProjectSecretEnvImportConflictStrategy.SkipExisting;

    public ProjectSecretImportExportViewModel(DesktopFeatureServices root, IProjectSecretEnvParser parser, IProjectSecretEnvWriter writer, IProjectSecretValueResolver resolver, Func<IReadOnlyList<ApiKeyEntry>> apiKeys)
    { _root = root; _parser = parser; _writer = writer; _resolver = resolver; _apiKeys = apiKeys; }

    public void Load(ProjectSecretInput project, bool enabled)
    {
        _project = project;
        IsEnabled = enabled;
        RefreshTargets(SelectedEnvironment?.Id, SelectedProfile?.Id);
        PreviewRows.Clear(); _parseResult = null; Status = Error = "";
    }

    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        foreach (var option in Environments) option.IsSelected = option == value;
        Profiles.Clear();
        var environment = value is null ? null : _project.Environments.FirstOrDefault(item => item.Id == value.Id);
        if (environment is not null)
            foreach (var profile in environment.Profiles.OrderBy(item => item.SortOrder)) Profiles.Add(new ProjectSecretProfileOption(environment.Id, profile.Id, profile.Name));
        SelectedProfile = Profiles.FirstOrDefault(); NotifyState();
    }
    partial void OnSelectedProfileChanged(ProjectSecretProfileOption? value)
    {
        foreach (var option in Profiles) option.IsSelected = option == value;
        NotifyState();
    }
    partial void OnIsEnabledChanged(bool value) => NotifyState();
    partial void OnIsBusyChanged(bool value) => NotifyState();
    partial void OnConflictStrategyChanged(ProjectSecretEnvImportConflictStrategy value)
    {
        OnPropertyChanged(nameof(ReplaceExisting));
        OnPropertyChanged(nameof(SkipExisting));
    }

    [RelayCommand] private void UseReplaceExisting() => ConflictStrategy = ProjectSecretEnvImportConflictStrategy.ReplaceExisting;
    [RelayCommand] private void UseSkipExisting() => ConflictStrategy = ProjectSecretEnvImportConflictStrategy.SkipExisting;

    [RelayCommand] private async Task PickImportFileAsync() { var path = await _root.PickOpenFileAsync("Import .env file", [".env", ".txt"], ".env files"); if (!string.IsNullOrWhiteSpace(path)) ImportPath = path; }
    [RelayCommand] private async Task PickExportFileAsync() { var path = await _root.PickSaveFileAsync("Export .env file", "project.env", ".env", [".env"], ".env file"); if (!string.IsNullOrWhiteSpace(path)) ExportPath = path; }

    [RelayCommand]
    private async Task PreviewImportAsync()
    {
        Error = Status = ""; PreviewRows.Clear();
        if (!CanOperate || string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath)) { Error = "Choose an existing .env file and target profile."; return; }
        _parseResult = _parser.Parse(await File.ReadAllTextAsync(ImportPath));
        var preview = _parser.BuildPreview(_parseResult, CurrentProfile()!.Variables.Select(variable => variable.Key).ToArray());
        foreach (var row in preview.Rows) PreviewRows.Add(row);
        Status = $"{preview.TotalRows} rows, {preview.ConflictRows} conflicts, {preview.InvalidRows + preview.DuplicateRows} issues.";
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanImport));
    }

    [RelayCommand]
    private async Task ApplyImportAsync()
    {
        if (!CanOperate || SelectedEnvironment is null || SelectedProfile is null) return;
        if (_parseResult is null) await PreviewImportAsync();
        if (_parseResult is null) return;
        IsBusy = true;
        try
        {
            var variables = CurrentProfile()!.Variables.ToList();
            foreach (var imported in _parseResult.Variables)
            {
                var index = variables.FindIndex(variable => string.Equals(variable.Key, imported.Key, StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && ConflictStrategy == ProjectSecretEnvImportConflictStrategy.SkipExisting) continue;
                var input = new ProjectSecretVariableInput(index >= 0 ? variables[index].Id : Guid.NewGuid().ToString("N"), imported.Key.Trim(), imported.Value, true, "", index >= 0 ? variables[index].SortOrder : variables.Count, ProjectSecretVariableSourceKind.ImportedEnvFile, "", "", "", DateTimeOffset.UtcNow.ToString("O"));
                if (index >= 0) variables[index] = input; else variables.Add(input);
            }
            var updated = ReplaceProfileVariables(variables);
            if (PersistRequested is not null) await PersistRequested(updated);
            _project = updated;
            Status = $"Imported {_parseResult.Variables.Count} variables into {SelectedEnvironment.Name} / {SelectedProfile.Name}.";
            _root.LogActivity("project_secrets", "Project Secrets .env imported", $"Imported {_parseResult.Variables.Count} variables into {_project.Name} / {SelectedEnvironment.Name} / {SelectedProfile.Name} from {Path.GetFileName(ImportPath)}.", "success", affectedItem: _project.Name);
            PreviewRows.Clear(); _parseResult = null; OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(CanImport));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task ExportValuesAsync() => ExportAsync(template: false);
    [RelayCommand] private Task ExportTemplateAsync() => ExportAsync(template: true);

    private async Task ExportAsync(bool template)
    {
        Error = Status = "";
        if (!CanOperate) return;
        if (string.IsNullOrWhiteSpace(ExportPath)) await PickExportFileAsync();
        if (string.IsNullOrWhiteSpace(ExportPath)) return;
        if (!template && !await _root.ConfirmAsync("Export plaintext .env", "This writes decrypted secrets to a plaintext file. Protect it and delete it when finished.", "Export")) return;
        var entries = CurrentProfile()!.Variables.Select(ToEntry).ToArray();
        var text = template ? _writer.WriteTemplate(entries) : _writer.WriteEnvironment(entries, variable => _resolver.Resolve(variable, _apiKeys()) ?? "");
        await File.WriteAllTextAsync(ExportPath, text);
        Status = template ? "Template exported." : "Values exported.";
        _root.LogActivity("project_secrets", template ? "Project Secrets .env template exported" : "Project Secrets .env exported", $"Exported {entries.Length} variables for {_project.Name} / {SelectedEnvironment?.Name} / {SelectedProfile?.Name} to {Path.GetFileName(ExportPath)}.", template ? "info" : "warning", affectedItem: _project.Name);
    }

    private void RefreshTargets(string? environmentId, string? profileId)
    {
        Environments.Clear(); foreach (var environment in _project.Environments.OrderBy(item => item.SortOrder)) Environments.Add(new(environment.Id, environment.Name));
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == environmentId) ?? Environments.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profileId) ?? Profiles.FirstOrDefault(); NotifyState();
    }
    private ProjectSecretProfileInput? CurrentProfile() => SelectedEnvironment is null || SelectedProfile is null ? null : _project.Environments.FirstOrDefault(environment => environment.Id == SelectedEnvironment.Id)?.Profiles.FirstOrDefault(profile => profile.Id == SelectedProfile.Id);
    private ProjectSecretInput ReplaceProfileVariables(IReadOnlyList<ProjectSecretVariableInput> variables) => _project with { Environments = _project.Environments.Select(environment => environment.Id != SelectedEnvironment!.Id ? environment : environment with { Profiles = environment.Profiles.Select(profile => profile.Id != SelectedProfile!.Id ? profile : profile with { Variables = variables }).ToArray() }).ToArray() };
    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariableInput variable) => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, variable.SortOrder, variable.SourceKind, variable.ReferencedItemId, variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc);
    private void NotifyState() { OnPropertyChanged(nameof(HasTarget)); OnPropertyChanged(nameof(CanOperate)); OnPropertyChanged(nameof(CanImport)); OnPropertyChanged(nameof(ShowUnavailable)); }
}

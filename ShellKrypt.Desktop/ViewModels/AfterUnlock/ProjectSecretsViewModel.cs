using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.ProjectSecrets;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ProjectSecretsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IProjectSecretService _projectSecretService;
    private readonly IApiKeyService _apiKeyService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly List<ProjectSecretRowVm> _all = new();
    private readonly List<ApiKeyEntry> _apiKeys = new();

    public ObservableCollection<ProjectSecretRowVm> Rows { get; } = new();
    public ObservableCollection<ProjectSecretEnvironmentOption> EnvironmentOptions { get; } = new();
    public ObservableCollection<ProjectSecretVariableRowVm> Variables { get; } = new();
    public ObservableCollection<ProjectSecretCompareRowVm> CompareRows { get; } = new();
    public ObservableCollection<ProjectSecretScanFindingVm> ScanFindings { get; } = new();
    public ObservableCollection<ProjectSecretApiKeyOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<ProjectSecretApiKeyFieldOption> ApiKeyFieldOptions { get; } = new();
    public IReadOnlyList<ProjectSecretEnvironmentKind> EnvironmentKindOptions { get; } = Enum.GetValues<ProjectSecretEnvironmentKind>();
    public IReadOnlyList<ProjectSecretVariableSourceKind> VariableSourceKindOptions { get; } = Enum.GetValues<ProjectSecretVariableSourceKind>();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private ProjectSecretRowVm? selectedProject;
    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private ProjectSecretVariableRowVm? selectedVariable;
    [ObservableProperty] private string projectName = "";
    [ObservableProperty] private string projectDescription = "";
    [ObservableProperty] private string projectNotes = "";
    [ObservableProperty] private string projectRootPath = "";
    [ObservableProperty] private string newEnvironmentName = "Development";
    [ObservableProperty] private ProjectSecretEnvironmentKind newEnvironmentKind = ProjectSecretEnvironmentKind.Development;
    [ObservableProperty] private string variableKey = "";
    [ObservableProperty] private string variableValue = "";
    [ObservableProperty] private bool variableIsSecret = true;
    [ObservableProperty] private string variableNotes = "";
    [ObservableProperty] private ProjectSecretVariableSourceKind variableSourceKind = ProjectSecretVariableSourceKind.Manual;
    [ObservableProperty] private ProjectSecretApiKeyOption? selectedApiKey;
    [ObservableProperty] private ProjectSecretApiKeyFieldOption? selectedApiKeyField;
    [ObservableProperty] private string importPath = "";
    [ObservableProperty] private string importPreview = "";
    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    private ProjectSecretEnvParseResult? _lastImportParse;

    public ProjectSecretsViewModel(
        MainWindowViewModel root,
        IProjectSecretService projectSecretService,
        IApiKeyService apiKeyService,
        Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _projectSecretService = projectSecretService;
        _apiKeyService = apiKeyService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        _ = LoadAsync();
    }

    public bool HasRows => Rows.Count > 0;
    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasSelectedEnvironment => SelectedEnvironment is not null;
    public bool HasVariables => Variables.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int TotalCount => _all.Count;
    public int TotalVariableCount => _all.Sum(row => row.VariableCount);
    public int TotalWarningCount => _all.Sum(row => row.WarningCount);
    public string LastScanSummary => SelectedProject?.Entry.LastScanResult is { } scan
        ? $"{scan.FilesScanned} files scanned, {scan.Findings.Count} finding(s)"
        : "No scan has been run for this project.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedProjectChanged(ProjectSecretRowVm? value)
    {
        PopulateProject(value?.Entry);
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(LastScanSummary));
    }

    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        LoadVariables();
        BuildCompare();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
    }

    partial void OnSelectedVariableChanged(ProjectSecretVariableRowVm? value)
    {
        if (value is null)
            return;

        VariableKey = value.Entry.Key;
        VariableValue = value.Entry.Value;
        VariableIsSecret = value.Entry.IsSecret;
        VariableNotes = value.Entry.Notes;
        VariableSourceKind = value.Entry.SourceKind;
    }

    partial void OnSelectedApiKeyChanged(ProjectSecretApiKeyOption? value)
    {
        ApiKeyFieldOptions.Clear();
        var apiKey = _apiKeys.FirstOrDefault(item => item.Id == value?.ItemId);
        if (apiKey is null)
            return;

        foreach (var field in apiKey.Fields)
            ApiKeyFieldOptions.Add(new ProjectSecretApiKeyFieldOption(field.Id, field.Label, field.Value, field.IsSensitive));

        SelectedApiKeyField = ApiKeyFieldOptions.FirstOrDefault();
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(LastScanSummary));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_root.VaultPath is null)
            return;

        IsBusy = true;
        Error = "";
        try
        {
            _all.Clear();
            Rows.Clear();
            _apiKeys.Clear();
            ApiKeyOptions.Clear();

            var projects = await _projectSecretService.ListAsync(_root.VaultPath, _root.VaultKey);
            foreach (var project in projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
                _all.Add(new ProjectSecretRowVm(project));

            _apiKeys.AddRange(await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey));
            foreach (var apiKey in _apiKeys.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                ApiKeyOptions.Add(new ProjectSecretApiKeyOption(apiKey.Id, apiKey.Name));

            ApplyFilter();
            SelectedProject ??= Rows.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddProject()
    {
        SelectedProject = null;
        ProjectName = "New Project";
        ProjectDescription = "";
        ProjectNotes = "";
        ProjectRootPath = "";
        EnvironmentOptions.Clear();
        EnvironmentOptions.Add(new ProjectSecretEnvironmentOption(Guid.NewGuid().ToString("N"), "Development"));
        SelectedEnvironment = EnvironmentOptions.First();
        Variables.Clear();
        SelectedVariable = null;
        ClearVariableForm();
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (_root.VaultPath is null)
            return;

        Error = "";
        try
        {
            var existing = SelectedProject?.Entry;
            var input = BuildProjectInput(existing);
            var saved = existing is null
                ? await _projectSecretService.AddAsync(_root.VaultPath, _root.VaultKey, input)
                : await _projectSecretService.UpdateAsync(_root.VaultPath, _root.VaultKey, existing.Id, existing.CreatedAtUtc, input);

            _root.LogActivity("project_secrets", existing is null ? "Project Secrets project created" : "Project Secrets project updated", $"{(existing is null ? "Created" : "Updated")} Project Secrets project {saved.Name}.", "success", affectedItem: saved.Name);
            await LoadAsync();
            SelectedProject = Rows.FirstOrDefault(row => row.Id == saved.Id);
            await _refreshAllItemsAsync(saved.Id);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (_root.VaultPath is null || SelectedProject is null)
            return;

        var name = SelectedProject.Name;
        var confirmed = await _root.ConfirmAsync("Delete Project Secrets project", $"Delete {name}? This removes its encrypted environments and variables.", "Delete", destructive: true);
        if (!confirmed)
            return;

        await _projectSecretService.DeleteAsync(_root.VaultPath, SelectedProject.Id);
        _root.LogActivity("project_secrets", "Project Secrets project deleted", $"Deleted Project Secrets project {name}.", "warning", affectedItem: name);
        SelectedProject = null;
        await LoadAsync();
        await _refreshAllItemsAsync(null);
    }

    [RelayCommand]
    private void AddEnvironment()
    {
        var name = string.IsNullOrWhiteSpace(NewEnvironmentName) ? NewEnvironmentKind.ToString() : NewEnvironmentName.Trim();
        if (EnvironmentOptions.Any(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Environment name already exists.";
            return;
        }

        EnvironmentOptions.Add(new ProjectSecretEnvironmentOption(Guid.NewGuid().ToString("N"), name));
        SelectedEnvironment = EnvironmentOptions.Last();
    }

    [RelayCommand]
    private void DeleteEnvironment()
    {
        if (SelectedEnvironment is null || EnvironmentOptions.Count <= 1)
            return;

        var remove = SelectedEnvironment;
        EnvironmentOptions.Remove(remove);
        SelectedEnvironment = EnvironmentOptions.FirstOrDefault();
    }

    [RelayCommand]
    private void AddOrUpdateVariable()
    {
        if (SelectedEnvironment is null)
            return;

        Error = "";
        try
        {
            var key = string.IsNullOrWhiteSpace(VariableKey) ? throw new InvalidOperationException("Variable key is required.") : VariableKey.Trim();
            if (Variables.Any(row => row != SelectedVariable && string.Equals(row.Key, key, StringComparison.Ordinal)))
                throw new InvalidOperationException("Variable key already exists in this environment.");

            var value = VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? "" : VariableValue;
            var linkedItemId = VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedApiKey?.ItemId ?? "" : "";
            var linkedFieldId = VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedApiKeyField?.FieldId ?? "" : "";
            var linkedFieldName = VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedApiKeyField?.Label ?? "" : "";
            if (VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey && (string.IsNullOrWhiteSpace(linkedItemId) || string.IsNullOrWhiteSpace(linkedFieldId)))
                throw new InvalidOperationException("Choose an API Key field to link.");

            var entry = new ProjectSecretVariableEntry(
                SelectedVariable?.Id ?? Guid.NewGuid().ToString("N"),
                key,
                value,
                VariableIsSecret,
                VariableNotes.Trim(),
                SelectedVariable?.Entry.SortOrder ?? Variables.Count,
                VariableSourceKind,
                linkedItemId,
                linkedFieldId,
                linkedFieldName,
                DateTimeOffset.UtcNow.ToString("O"));

            if (SelectedVariable is null)
                Variables.Add(new ProjectSecretVariableRowVm(entry));
            else
                SelectedVariable.Update(entry);

            ClearVariableForm();
            BuildCompare();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteVariable()
    {
        if (SelectedVariable is null)
            return;

        Variables.Remove(SelectedVariable);
        SelectedVariable = null;
        ClearVariableForm();
        BuildCompare();
    }

    [RelayCommand]
    private void SelectVariable(ProjectSecretVariableRowVm? variable)
    {
        SelectedVariable = variable;
    }

    [RelayCommand]
    private void RevealVariable(ProjectSecretVariableRowVm? variable)
    {
        if (variable is not null)
            variable.IsValueRevealed = !variable.IsValueRevealed;
    }

    [RelayCommand]
    private async Task CopyVariableAsync(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        var value = ResolveVariableValue(variable.Entry);
        await _root.CopyToClipboardAsync(value);
        _root.LogActivity("project_secrets", "Project variable copied", $"Copied {variable.Key}.", "info", affectedItem: variable.Key);
    }

    [RelayCommand]
    private async Task PickImportFileAsync()
    {
        var path = await _root.PickOpenFileAsync("Import .env file", [".env", ".txt"], ".env files");
        if (!string.IsNullOrWhiteSpace(path))
            ImportPath = path;
    }

    [RelayCommand]
    private async Task PreviewImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath))
            return;

        _lastImportParse = EnvFileParser.Parse(await File.ReadAllTextAsync(ImportPath));
        var preview = EnvFileParser.BuildPreview(_lastImportParse, Variables.Select(variable => variable.Key).ToArray());
        ImportPreview = $"{preview.TotalRows} row(s), {preview.ConflictRows} conflict(s), {_lastImportParse.Issues.Count} issue(s)";
    }

    [RelayCommand]
    private async Task ApplyImportAsync()
    {
        if (_lastImportParse is null)
            await PreviewImportAsync();

        if (_lastImportParse is null)
            return;

        var existing = Variables.ToDictionary(variable => variable.Key, StringComparer.Ordinal);
        foreach (var variable in _lastImportParse.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Key))
                continue;

            var entry = new ProjectSecretVariableEntry(
                existing.TryGetValue(variable.Key, out var current) ? current.Id : Guid.NewGuid().ToString("N"),
                variable.Key.Trim(),
                variable.Value,
                true,
                "",
                existing.TryGetValue(variable.Key, out current) ? current.Entry.SortOrder : Variables.Count,
                ProjectSecretVariableSourceKind.ImportedEnvFile,
                "",
                "",
                "",
                DateTimeOffset.UtcNow.ToString("O"));

            if (existing.TryGetValue(variable.Key, out current))
                current.Update(entry);
            else
                Variables.Add(new ProjectSecretVariableRowVm(entry));
        }

        ImportPreview = $"Imported {_lastImportParse.Variables.Count} variable(s).";
        _root.LogActivity("project_secrets", "Project Secrets .env imported", $"Imported {_lastImportParse.Variables.Count} variables into {ProjectName} / {SelectedEnvironment?.Name}.", "success", affectedItem: ProjectName);
        await SaveProjectAsync();
    }

    [RelayCommand]
    private async Task PickExportFileAsync()
    {
        var path = await _root.PickSaveFileAsync("Export .env file", $"{SafeFileName(ProjectName)}.env", ".env", [".env"], ".env file");
        if (!string.IsNullOrWhiteSpace(path))
            ExportPath = path;
    }

    [RelayCommand]
    private async Task ExportEnvAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
            await PickExportFileAsync();

        if (string.IsNullOrWhiteSpace(ExportPath))
            return;

        var confirmed = await _root.ConfirmAsync(
            "Export plaintext .env",
            "This export writes decrypted secrets to a plaintext .env file. Anyone with access to that file can read them. Delete the file when you no longer need it.",
            "Export");
        if (!confirmed)
            return;

        await File.WriteAllTextAsync(ExportPath, EnvFileWriter.WriteEnvironment(Variables.Select(row => row.Entry), ResolveVariableValue));
        _root.LogActivity("project_secrets", "Project Secrets .env exported", $"Exported {Variables.Count} variables for {ProjectName} / {SelectedEnvironment?.Name} to {Path.GetFileName(ExportPath)}.", "warning", affectedItem: ProjectName);
    }

    [RelayCommand]
    private async Task ExportTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
            await PickExportFileAsync();

        if (string.IsNullOrWhiteSpace(ExportPath))
            return;

        await File.WriteAllTextAsync(ExportPath, EnvFileWriter.WriteTemplate(Variables.Select(row => row.Entry)));
        _root.LogActivity("project_secrets", "Project Secrets .env template exported", $"Exported .env template for {ProjectName} / {SelectedEnvironment?.Name} to {Path.GetFileName(ExportPath)}.", "info", affectedItem: ProjectName);
    }

    [RelayCommand]
    private async Task PickProjectRootAsync()
    {
        var path = await _root.PickFolderAsync("Choose project root");
        if (!string.IsNullOrWhiteSpace(path))
            ProjectRootPath = path;
    }

    [RelayCommand]
    private async Task ScanProjectAsync()
    {
        if (SelectedProject is null)
            return;

        var root = string.IsNullOrWhiteSpace(ProjectRootPath) ? SelectedProject.ProjectRootPath : ProjectRootPath;
        var variables = EnvironmentOptions
            .SelectMany(option => option.Id == SelectedEnvironment?.Id ? Variables.Select(row => row.Entry) : SelectedProject.Entry.Environments.FirstOrDefault(environment => environment.Id == option.Id)?.Variables ?? Array.Empty<ProjectSecretVariableEntry>())
            .ToArray();
        var secrets = variables
            .Where(variable => variable.IsSecret && variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey && !string.IsNullOrWhiteSpace(variable.Value))
            .ToDictionary(variable => variable.Key, variable => variable.Value, StringComparer.Ordinal);

        var scanner = new ProjectSecretFilesystemScanner();
        var result = scanner.Scan(new ProjectSecretScanRequest(SelectedProject.Id, root, variables.Select(variable => variable.Key).ToArray(), secrets));
        ScanFindings.Clear();
        foreach (var finding in result.Findings)
            ScanFindings.Add(new ProjectSecretScanFindingVm(finding));

        var input = BuildProjectInput(SelectedProject.Entry) with { LastScanResult = result };
        var saved = await _projectSecretService.UpdateAsync(_root.VaultPath!, _root.VaultKey, SelectedProject.Id, SelectedProject.Entry.CreatedAtUtc, input);
        SelectedProject.Update(saved);
        _root.LogActivity("project_secrets", "Project folder scanned", $"Scanned project folder for {saved.Name}: {result.FilesScanned} files scanned, {result.Findings.Count} findings.", "info", affectedItem: saved.Name);
        OnPropertyChanged(nameof(LastScanSummary));
    }

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (_all.Count == 0)
            await LoadAsync();

        SelectedProject = Rows.FirstOrDefault(row => row.Id == itemId) ?? _all.FirstOrDefault(row => row.Id == itemId);
        return SelectedProject is not null;
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        var query = SearchText.Trim();
        foreach (var row in _all.Where(row => string.IsNullOrWhiteSpace(query)
                                              || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                              || row.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                                              || row.ProjectRootPath.Contains(query, StringComparison.OrdinalIgnoreCase)))
            Rows.Add(row);

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TotalVariableCount));
        OnPropertyChanged(nameof(TotalWarningCount));
    }

    private void PopulateProject(ProjectSecretEntry? project)
    {
        ProjectName = project?.Name ?? "";
        ProjectDescription = project?.Description ?? "";
        ProjectNotes = project?.Notes ?? "";
        ProjectRootPath = project?.ProjectRootPath ?? "";
        EnvironmentOptions.Clear();
        ScanFindings.Clear();

        foreach (var environment in project is null
                     ? Array.Empty<ProjectSecretEnvironmentEntry>()
                     : project.Environments.OrderBy(environment => environment.SortOrder).ToArray())
            EnvironmentOptions.Add(new ProjectSecretEnvironmentOption(environment.Id, environment.Name));

        if (EnvironmentOptions.Count == 0)
            EnvironmentOptions.Add(new ProjectSecretEnvironmentOption(Guid.NewGuid().ToString("N"), "Development"));

        SelectedEnvironment = EnvironmentOptions.FirstOrDefault();
        if (project?.LastScanResult is { } scan)
        {
            foreach (var finding in scan.Findings)
                ScanFindings.Add(new ProjectSecretScanFindingVm(finding));
        }
    }

    private void LoadVariables()
    {
        Variables.Clear();
        var environment = SelectedProject?.Entry.Environments.FirstOrDefault(environment => environment.Id == SelectedEnvironment?.Id);
        if (environment is not null)
        {
            foreach (var variable in environment.Variables.OrderBy(variable => variable.SortOrder))
                Variables.Add(new ProjectSecretVariableRowVm(variable));
        }

        ClearVariableForm();
        OnPropertyChanged(nameof(HasVariables));
    }

    private void BuildCompare()
    {
        CompareRows.Clear();
        if (SelectedProject is null)
            return;

        var project = BuildProjectEntrySnapshot(SelectedProject.Entry);
        foreach (var row in ProjectSecretComparer.Compare(project).Rows)
            CompareRows.Add(new ProjectSecretCompareRowVm(row.VariableKey, row.Cells));
    }

    private ProjectSecretInput BuildProjectInput(ProjectSecretEntry? existing)
        => new(
            ProjectName,
            ProjectDescription,
            ProjectNotes,
            string.IsNullOrWhiteSpace(ProjectRootPath) ? null : ProjectRootPath,
            BuildEnvironmentInputs(existing),
            existing?.LinkedApiKeys.Select(link => new ProjectSecretLinkedApiKeyInput(link.Id, link.ApiKeyItemId, link.ApiKeyFieldId, link.VariableKey, link.EnvironmentId, link.ImportCopy)).ToArray() ?? Array.Empty<ProjectSecretLinkedApiKeyInput>(),
            existing?.LastScanResult);

    private IReadOnlyList<ProjectSecretEnvironmentInput> BuildEnvironmentInputs(ProjectSecretEntry? existing)
        => EnvironmentOptions.Select((option, index) =>
        {
            var existingEnvironment = existing?.Environments.FirstOrDefault(environment => environment.Id == option.Id);
            var variables = option.Id == SelectedEnvironment?.Id
                ? Variables.Select((row, variableIndex) => ToInput(row.Entry, variableIndex)).ToArray()
                : existingEnvironment?.Variables.Select((variable, variableIndex) => ToInput(variable, variableIndex)).ToArray() ?? Array.Empty<ProjectSecretVariableInput>();

            return new ProjectSecretEnvironmentInput(
                option.Id,
                option.Name,
                existingEnvironment?.Kind ?? InferEnvironmentKind(option.Name),
                variables,
                existingEnvironment?.Notes ?? "",
                index);
        }).ToArray();

    private ProjectSecretEntry BuildProjectEntrySnapshot(ProjectSecretEntry existing)
        => existing with
        {
            Name = ProjectName,
            Description = ProjectDescription,
            Notes = ProjectNotes,
            ProjectRootPath = string.IsNullOrWhiteSpace(ProjectRootPath) ? null : ProjectRootPath,
            Environments = BuildEnvironmentInputs(existing).Select(environment => new ProjectSecretEnvironmentEntry(
                environment.Id,
                environment.Name,
                environment.Kind,
                environment.Variables.Select(variable => new ProjectSecretVariableEntry(
                    variable.Id,
                    variable.Key,
                    variable.Value,
                    variable.IsSecret,
                    variable.Notes,
                    variable.SortOrder,
                    variable.SourceKind,
                    variable.LinkedItemId,
                    variable.LinkedFieldId,
                    variable.LinkedFieldName,
                    variable.LastUpdatedAtUtc)).ToArray(),
                environment.Notes,
                environment.SortOrder)).ToArray()
        };

    private static ProjectSecretVariableInput ToInput(ProjectSecretVariableEntry variable, int sortOrder)
        => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, sortOrder, variable.SourceKind, variable.LinkedItemId, variable.LinkedFieldId, variable.LinkedFieldName, variable.LastUpdatedAtUtc);

    private string ResolveVariableValue(ProjectSecretVariableEntry variable)
        => ProjectSecretService.ResolveLinkedApiKeyValue(_apiKeys, variable);

    private void ClearVariableForm()
    {
        SelectedVariable = null;
        VariableKey = "";
        VariableValue = "";
        VariableIsSecret = true;
        VariableNotes = "";
        VariableSourceKind = ProjectSecretVariableSourceKind.Manual;
    }

    private static ProjectSecretEnvironmentKind InferEnvironmentKind(string name)
        => Enum.TryParse<ProjectSecretEnvironmentKind>(name, true, out var kind) ? kind : ProjectSecretEnvironmentKind.Custom;

    private static string SafeFileName(string value)
        => string.Join("_", (string.IsNullOrWhiteSpace(value) ? "project" : value).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}

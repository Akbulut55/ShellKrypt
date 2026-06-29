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
    private readonly Dictionary<string, List<ProjectSecretVariableEntry>> _environmentVariableDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sessionSelectedProfilesByEnvironment = new(StringComparer.OrdinalIgnoreCase);
    private string? _loadedEnvironmentId;
    private bool _syncingEnvironmentSelection;

    public ObservableCollection<ProjectSecretRowVm> Rows { get; } = new();
    public ObservableCollection<string> EnvironmentNameOptions { get; } = new();
    public ObservableCollection<string> ProfileNameOptions { get; } = new();
    public ObservableCollection<ProjectSecretNameChipVm> EnvironmentNameChips { get; } = new();
    public ObservableCollection<ProjectSecretNameChipVm> ProfileNameChips { get; } = new();
    public ObservableCollection<ProjectSecretEnvironmentOption> EnvironmentOptions { get; } = new();
    public ObservableCollection<ProjectSecretVariableRowVm> Variables { get; } = new();
    public ObservableCollection<ProjectSecretCompareRowVm> CompareRows { get; } = new();
    public ObservableCollection<ProjectSecretScanFindingVm> ScanFindings { get; } = new();
    public ObservableCollection<ProjectSecretApiKeyOption> ApiKeyOptions { get; } = new();
    public ObservableCollection<ProjectSecretApiKeyFieldOption> ApiKeyFieldOptions { get; } = new();
    public ObservableCollection<ProjectSecretApiKeyLinkOption> ApiKeyLinkOptions { get; } = new();
    public ObservableCollection<ProjectSecretProfileSelectionVm> AddEnvironmentProfiles { get; } = new();
    public ObservableCollection<string> EnvironmentDetailProfileOptions { get; } = new();
    public IReadOnlyList<ProjectSecretVariableSourceKind> VariableSourceKindOptions { get; } =
    [
        ProjectSecretVariableSourceKind.Manual,
        ProjectSecretVariableSourceKind.LinkedApiKey
    ];

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private ProjectSecretRowVm? selectedProject;
    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private ProjectSecretVariableRowVm? selectedVariable;
    [ObservableProperty] private string projectName = "";
    [ObservableProperty] private string projectDescription = "";
    [ObservableProperty] private string projectNotes = "";
    [ObservableProperty] private string projectRootPath = "";
    [ObservableProperty] private string selectedEnvironmentName = "";
    [ObservableProperty] private string selectedProfileName = "";
    [ObservableProperty] private string newEnvironmentName = "";
    [ObservableProperty] private string newProfileName = "";
    [ObservableProperty] private bool isAddEnvironmentModalOpen;
    [ObservableProperty] private bool isEnvironmentManagerOpen;
    [ObservableProperty] private string selectedEnvironmentDetailName = "";
    [ObservableProperty] private bool isEditingEnvironmentDetailName;
    [ObservableProperty] private string environmentDetailEditName = "";
    [ObservableProperty] private string editingProfileName = "";
    [ObservableProperty] private string profileEditName = "";
    [ObservableProperty] private string variableKey = "";
    [ObservableProperty] private string variableValue = "";
    [ObservableProperty] private bool variableIsSecret = true;
    [ObservableProperty] private string variableNotes = "";
    [ObservableProperty] private ProjectSecretVariableSourceKind variableSourceKind = ProjectSecretVariableSourceKind.Manual;
    [ObservableProperty] private ProjectSecretApiKeyOption? selectedApiKey;
    [ObservableProperty] private ProjectSecretApiKeyFieldOption? selectedApiKeyField;
    [ObservableProperty] private string apiKeyLinkSearchText = "";
    [ObservableProperty] private string importPath = "";
    [ObservableProperty] private string importPreview = "";
    [ObservableProperty] private int importNewCount;
    [ObservableProperty] private int importConflictCount;
    [ObservableProperty] private int importInvalidCount;
    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isProjectEditing;

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
    public bool CanEditVariables => IsProjectEditing && HasSelectedEnvironment;
    public bool HasSelectedEnvironmentDetail => !string.IsNullOrWhiteSpace(SelectedEnvironmentDetailName);
    public bool IsEditingProfileName => !string.IsNullOrWhiteSpace(EditingProfileName);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasProjectDescription => !string.IsNullOrWhiteSpace(ProjectDescription);
    public bool HasSelectedVariable => SelectedVariable is not null;
    public bool IsProjectReadOnly => !IsProjectEditing;
    public bool IsLinkedApiKeySource => VariableSourceKind == ProjectSecretVariableSourceKind.LinkedApiKey;
    public bool IsManualVariableSource => !IsLinkedApiKeySource;
    public bool IsVariableValueReadOnly => IsProjectReadOnly || IsLinkedApiKeySource;
    public string VariableSubmitLabel => SelectedVariable is null ? "Add" : "Update";
    public IReadOnlyList<ProjectSecretApiKeyLinkOption> FilteredApiKeyLinkOptions
    {
        get
        {
            var query = ApiKeyLinkSearchText.Trim();
            return ApiKeyLinkOptions
                .Where(option => string.IsNullOrWhiteSpace(query)
                                 || option.ApiKeyName.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || option.FieldLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
    public int TotalCount => _all.Count;
    public int TotalVariableCount => _all.Sum(row => row.VariableCount);
    public int TotalWarningCount => _all.Sum(row => row.WarningCount);
    public bool HasImportPreview => ImportNewCount > 0 || ImportConflictCount > 0 || ImportInvalidCount > 0 || !string.IsNullOrWhiteSpace(ImportPreview);
    public string ProjectRootDisplay => string.IsNullOrWhiteSpace(ProjectRootPath) ? "No project root selected" : ProjectRootPath;
    public IReadOnlyList<ProjectSecretScanFindingVm> PlaintextLeakFindings => ScanFindings.Where(finding => finding.Finding.Kind == ProjectSecretScanFindingKind.PossiblePlaintextLeak).ToArray();
    public IReadOnlyList<ProjectSecretScanFindingVm> MissingReferenceFindings => ScanFindings.Where(finding => finding.Finding.Kind == ProjectSecretScanFindingKind.ReferencedButMissingVariable).ToArray();
    public IReadOnlyList<ProjectSecretScanFindingVm> UnusedVariableFindings => ScanFindings.Where(finding => finding.Finding.Kind == ProjectSecretScanFindingKind.UnusedVariable).ToArray();
    public IReadOnlyList<ProjectSecretScanFindingVm> ScannerWarningFindings => ScanFindings.Where(finding => finding.Finding.Kind is ProjectSecretScanFindingKind.EnvFileWithValuesDetected or ProjectSecretScanFindingKind.SkippedLargeFile or ProjectSecretScanFindingKind.ScanLimitReached or ProjectSecretScanFindingKind.BrokenProjectRoot).ToArray();
    public string LastScanSummary => SelectedProject?.Entry.LastScanResult is { } scan
        ? $"{scan.FilesScanned} files scanned, {scan.Findings.Count} finding(s)"
        : "No scan has been run for this project.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedProjectChanged(ProjectSecretRowVm? value)
    {
        PopulateProject(value?.Entry);
        if (value is not null)
            IsProjectEditing = false;
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(LastScanSummary));
    }

    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        if (_syncingEnvironmentSelection)
            return;

        SaveCurrentVariablesToDraft();
        if (value is not null)
        {
            _syncingEnvironmentSelection = true;
            SelectedEnvironmentName = value.Name;
            SelectedProfileName = value.StageLabel;
            _syncingEnvironmentSelection = false;
        }

        _loadedEnvironmentId = value?.Id;
        LoadVariablesFromDraft(value);
        BuildCompare();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanEditVariables));
    }

    partial void OnSelectedEnvironmentNameChanged(string value)
    {
        RefreshSelectedChips();
        if (!_syncingEnvironmentSelection)
            SelectEnvironmentScope(createIfMissing: false);
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        RefreshSelectedChips();
        if (!_syncingEnvironmentSelection)
            SelectEnvironmentScope(createIfMissing: false);
    }

    partial void OnSelectedVariableChanged(ProjectSecretVariableRowVm? value)
    {
        OnPropertyChanged(nameof(HasSelectedVariable));
        OnPropertyChanged(nameof(VariableSubmitLabel));
    }

    partial void OnSelectedApiKeyChanged(ProjectSecretApiKeyOption? value)
    {
        ApiKeyFieldOptions.Clear();
        SelectedApiKeyField = null;
        var apiKey = _apiKeys.FirstOrDefault(item => item.Id == value?.ItemId);
        if (apiKey is null)
            return;

        foreach (var field in apiKey.Fields)
            ApiKeyFieldOptions.Add(new ProjectSecretApiKeyFieldOption(field.Id, field.Label, field.Value, field.IsSensitive));

        SelectedApiKeyField = ApiKeyFieldOptions.FirstOrDefault();
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnProjectDescriptionChanged(string value) => OnPropertyChanged(nameof(HasProjectDescription));
    partial void OnIsProjectEditingChanged(bool value)
    {
        foreach (var variable in Variables)
            variable.OwnerIsProjectEditing = value;

        OnPropertyChanged(nameof(IsProjectReadOnly));
        OnPropertyChanged(nameof(IsVariableValueReadOnly));
        OnPropertyChanged(nameof(CanEditVariables));
    }

    partial void OnVariableSourceKindChanged(ProjectSecretVariableSourceKind value)
    {
        if (value != ProjectSecretVariableSourceKind.LinkedApiKey)
            ClearLinkedApiKeySelection();

        OnPropertyChanged(nameof(IsLinkedApiKeySource));
        OnPropertyChanged(nameof(IsManualVariableSource));
        OnPropertyChanged(nameof(IsVariableValueReadOnly));
    }
    partial void OnImportPreviewChanged(string value) => OnPropertyChanged(nameof(HasImportPreview));
    partial void OnImportNewCountChanged(int value) => OnPropertyChanged(nameof(HasImportPreview));
    partial void OnImportConflictCountChanged(int value) => OnPropertyChanged(nameof(HasImportPreview));
    partial void OnImportInvalidCountChanged(int value) => OnPropertyChanged(nameof(HasImportPreview));
    partial void OnProjectRootPathChanged(string value) => OnPropertyChanged(nameof(ProjectRootDisplay));
    partial void OnApiKeyLinkSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredApiKeyLinkOptions));
    partial void OnSelectedEnvironmentDetailNameChanged(string value)
    {
        IsEditingEnvironmentDetailName = false;
        EnvironmentDetailEditName = value;
        EditingProfileName = "";
        ProfileEditName = "";
        RefreshEnvironmentDetailProfiles();
        OnPropertyChanged(nameof(HasSelectedEnvironmentDetail));
    }

    partial void OnEditingProfileNameChanged(string value) => OnPropertyChanged(nameof(IsEditingProfileName));

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(LastScanSummary));
    }

    [RelayCommand]
    private void SelectProject(ProjectSecretRowVm? project)
    {
        if (project is not null)
            SelectedProject = project;
    }

    [RelayCommand]
    private void EditProject()
    {
        IsProjectEditing = true;
    }

    [RelayCommand]
    private void CancelProjectEdit()
    {
        if (SelectedProject is null)
        {
            InitializeNewProjectEditor();
            return;
        }

        PopulateProject(SelectedProject.Entry);
        IsProjectEditing = false;
        ClearVariableForm();
        foreach (var variable in Variables)
            variable.CancelEdit();
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
            ApiKeyLinkOptions.Clear();

            var projects = await _projectSecretService.ListAsync(_root.VaultPath, _root.VaultKey);
            foreach (var project in projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
                _all.Add(new ProjectSecretRowVm(project));

            _apiKeys.AddRange(await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey));
            foreach (var apiKey in _apiKeys.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                ApiKeyOptions.Add(new ProjectSecretApiKeyOption(apiKey.Id, apiKey.Name));
                foreach (var field in apiKey.Fields.OrderBy(field => field.SortOrder))
                    ApiKeyLinkOptions.Add(new ProjectSecretApiKeyLinkOption(apiKey.Id, apiKey.Name, field.Id, field.Label, field.IsSensitive));
            }
            OnPropertyChanged(nameof(FilteredApiKeyLinkOptions));

            ApplyFilter();
            if (Rows.Count == 0)
            {
                InitializeNewProjectEditor();
            }
            else if (SelectedProject is null || Rows.All(row => row.Id != SelectedProject.Id))
            {
                SelectedProject = Rows.First();
                IsProjectEditing = false;
            }
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
        => InitializeNewProjectEditor();

    private void InitializeNewProjectEditor()
    {
        SelectedProject = null;
        ProjectName = "New Project";
        ProjectDescription = "";
        ProjectNotes = "";
        ProjectRootPath = "";
        EnvironmentNameOptions.Clear();
        EnvironmentNameChips.Clear();
        ProfileNameChips.Clear();
        ProfileNameOptions.Clear();
        EnvironmentOptions.Clear();
        _environmentVariableDrafts.Clear();
        _loadedEnvironmentId = null;
        SelectedEnvironmentName = "";
        SelectedProfileName = "";
        Variables.Clear();
        SelectedEnvironment = null;
        SelectedVariable = null;
        ClearVariableForm();
        IsProjectEditing = true;
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (_root.VaultPath is null)
            return;

        Error = "";
        try
        {
            var trimmedName = ProjectName.Trim();
            if (_all.Any(project => project.Id != SelectedProject?.Id && string.Equals(project.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                Error = "Project name already exists.";
                return;
            }

            SaveCurrentVariablesToDraft();
            var existing = SelectedProject?.Entry;
            var input = BuildProjectInput(existing);
            var saved = existing is null
                ? await _projectSecretService.AddAsync(_root.VaultPath, _root.VaultKey, input)
                : await _projectSecretService.UpdateAsync(_root.VaultPath, _root.VaultKey, existing.Id, existing.CreatedAtUtc, input);

            _root.LogActivity("project_secrets", existing is null ? "Project Secrets project created" : "Project Secrets project updated", $"{(existing is null ? "Created" : "Updated")} Project Secrets project {saved.Name}.", "success", affectedItem: saved.Name);
            await LoadAsync();
            SelectedProject = Rows.FirstOrDefault(row => row.Id == saved.Id);
            IsProjectEditing = false;
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
    private void SelectEnvironment(ProjectSecretEnvironmentOption? environment)
    {
        if (environment is not null)
            SelectedEnvironment = environment;
    }

    [RelayCommand]
    private void SelectEnvironmentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var normalized = name.Trim();
        var profile = ResolvePreferredProfileName(normalized);
        if (string.IsNullOrWhiteSpace(profile))
            return;

        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = normalized;
        SelectedProfileName = profile;
        _syncingEnvironmentSelection = false;
        RefreshSelectedChips();
        SelectEnvironmentScope(createIfMissing: false);
    }

    [RelayCommand]
    private void SelectProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
            return;

        var normalized = profile.Trim();
        var environmentName = SelectedEnvironmentName;
        var environment = FindEnvironmentProfile(environmentName, normalized)
                          ?? ResolveFirstProfileForEnvironment(environmentName);
        if (environment is null)
            return;

        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = environment.Name;
        SelectedProfileName = environment.StageLabel;
        SelectedEnvironment = environment;
        _syncingEnvironmentSelection = false;
        RememberSelectedProfile(environment.Name, environment.StageLabel);
        _loadedEnvironmentId = environment.Id;
        LoadVariablesFromDraft(environment);
        RefreshSelectedChips();
        BuildCompare();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanEditVariables));
    }

    [RelayCommand]
    private void OpenEnvironmentManager()
    {
        Error = "";
        SelectedEnvironmentDetailName = EnvironmentNameOptions.FirstOrDefault(name => string.Equals(name, SelectedEnvironmentName, StringComparison.OrdinalIgnoreCase))
                                        ?? EnvironmentNameOptions.FirstOrDefault()
                                        ?? "";
        RefreshEnvironmentDetailProfiles();
        NewProfileName = "";
        IsEnvironmentManagerOpen = true;
    }

    [RelayCommand]
    private void CloseEnvironmentManager()
    {
        IsEnvironmentManagerOpen = false;
        SelectedEnvironmentDetailName = "";
        EnvironmentDetailProfileOptions.Clear();
        NewProfileName = "";
        IsEditingEnvironmentDetailName = false;
        EditingProfileName = "";
        ProfileEditName = "";
    }

    [RelayCommand]
    private void OpenAddEnvironmentModal()
    {
        Error = "";
        NewEnvironmentName = "";
        NewProfileName = "";
        AddEnvironmentProfiles.Clear();

        IsAddEnvironmentModalOpen = true;
    }

    [RelayCommand]
    private void CancelAddEnvironment()
    {
        IsAddEnvironmentModalOpen = false;
        AddEnvironmentProfiles.Clear();
        NewProfileName = "";
    }

    [RelayCommand]
    private void AddEnvironmentProfile()
    {
        var profileName = NewProfileName.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
            return;

        if (AddEnvironmentProfiles.Any(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Profile name already exists in this environment.";
            return;
        }

        AddEnvironmentProfiles.Add(new ProjectSecretProfileSelectionVm(profileName));
        NewProfileName = "";
        Error = "";
    }

    [RelayCommand]
    private void RemoveAddEnvironmentProfile(ProjectSecretProfileSelectionVm? profile)
    {
        if (profile is not null)
            AddEnvironmentProfiles.Remove(profile);
    }

    [RelayCommand]
    private void AddEnvironment()
    {
        var name = string.IsNullOrWhiteSpace(NewEnvironmentName) ? "Environment" : NewEnvironmentName.Trim();
        if (EnvironmentNameOptions.Any(option => string.Equals(option, name, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Environment name already exists.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(NewProfileName))
            AddEnvironmentProfile();

        var selectedProfiles = AddEnvironmentProfiles
            .Select(profile => profile.Name)
            .Where(profile => !string.IsNullOrWhiteSpace(profile))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedProfiles.Length == 0)
        {
            Error = "Add at least one profile.";
            return;
        }

        EnvironmentNameOptions.Add(name);
        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = name;
        SelectedEnvironmentDetailName = name;
        _syncingEnvironmentSelection = false;
        foreach (var profile in selectedProfiles)
            CreateEnvironmentProfile(name, profile);

        SelectedProfileName = selectedProfiles[0];
        RefreshEnvironmentNameChips();
        SelectEnvironmentScope(createIfMissing: false);
        RefreshEnvironmentDetailProfiles();
        IsAddEnvironmentModalOpen = false;
        AddEnvironmentProfiles.Clear();
        NewProfileName = "";
    }

    [RelayCommand]
    private void DeleteEnvironment()
        => DeleteEnvironmentByName(SelectedEnvironmentName);

    [RelayCommand]
    private void SelectEnvironmentDetail(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            SelectedEnvironmentDetailName = name.Trim();
    }

    [RelayCommand]
    private void BeginRenameEnvironment()
    {
        if (string.IsNullOrWhiteSpace(SelectedEnvironmentDetailName))
            return;

        EnvironmentDetailEditName = SelectedEnvironmentDetailName;
        IsEditingEnvironmentDetailName = true;
    }

    [RelayCommand]
    private void CancelRenameEnvironment()
    {
        EnvironmentDetailEditName = SelectedEnvironmentDetailName;
        IsEditingEnvironmentDetailName = false;
    }

    [RelayCommand]
    private void SaveRenameEnvironment()
    {
        var oldName = SelectedEnvironmentDetailName.Trim();
        var newName = EnvironmentDetailEditName.Trim();
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return;

        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase) &&
            EnvironmentNameOptions.Any(option => string.Equals(option, newName, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Environment name already exists.";
            return;
        }

        for (var index = 0; index < EnvironmentOptions.Count; index++)
        {
            var option = EnvironmentOptions[index];
            if (string.Equals(option.Name, oldName, StringComparison.OrdinalIgnoreCase))
                EnvironmentOptions[index] = option with { Name = newName };
        }

        var nameIndex = EnvironmentNameOptions.IndexOf(oldName);
        if (nameIndex >= 0)
            EnvironmentNameOptions[nameIndex] = newName;

        _syncingEnvironmentSelection = true;
        if (string.Equals(SelectedEnvironmentName, oldName, StringComparison.OrdinalIgnoreCase))
            SelectedEnvironmentName = newName;
        SelectedEnvironmentDetailName = newName;
        _syncingEnvironmentSelection = false;

        IsEditingEnvironmentDetailName = false;
        Error = "";
        RefreshProfileNameOptions();
        RefreshEnvironmentDetailProfiles();
        SelectEnvironmentScope(createIfMissing: false);
    }

    [RelayCommand]
    private void BeginRenameProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return;

        EditingProfileName = profileName.Trim();
        ProfileEditName = EditingProfileName;
    }

    [RelayCommand]
    private void CancelRenameProfile()
    {
        EditingProfileName = "";
        ProfileEditName = "";
    }

    [RelayCommand]
    private void SaveRenameProfile()
    {
        var environmentName = SelectedEnvironmentDetailName.Trim();
        var oldProfile = EditingProfileName.Trim();
        var newProfile = ProfileEditName.Trim();
        if (string.IsNullOrWhiteSpace(environmentName) || string.IsNullOrWhiteSpace(oldProfile) || string.IsNullOrWhiteSpace(newProfile))
            return;

        if (!string.Equals(oldProfile, newProfile, StringComparison.OrdinalIgnoreCase) &&
            EnvironmentOptions.Any(option =>
                string.Equals(option.Name, environmentName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.StageLabel, newProfile, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Profile name already exists in this environment.";
            return;
        }

        for (var index = 0; index < EnvironmentOptions.Count; index++)
        {
            var option = EnvironmentOptions[index];
            if (string.Equals(option.Name, environmentName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.StageLabel, oldProfile, StringComparison.OrdinalIgnoreCase))
                EnvironmentOptions[index] = option with { ProfileName = newProfile, Kind = InferEnvironmentKind(newProfile) };
        }

        _syncingEnvironmentSelection = true;
        if (string.Equals(SelectedEnvironmentName, environmentName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(SelectedProfileName, oldProfile, StringComparison.OrdinalIgnoreCase))
            SelectedProfileName = newProfile;
        _syncingEnvironmentSelection = false;

        EditingProfileName = "";
        ProfileEditName = "";
        Error = "";
        RefreshProfileNameOptions();
        RefreshEnvironmentDetailProfiles();
        SelectEnvironmentScope(createIfMissing: false);
    }

    [RelayCommand]
    private void AddProfileToEnvironmentDetail()
    {
        var environmentName = SelectedEnvironmentDetailName.Trim();
        var profileName = NewProfileName.Trim();
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            Error = "Select an environment first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(profileName))
            return;

        if (EnvironmentOptions.Any(option =>
                string.Equals(option.Name, environmentName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.StageLabel, profileName, StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Profile name already exists in this environment.";
            return;
        }

        CreateEnvironmentProfile(environmentName, profileName);
        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = environmentName;
        SelectedProfileName = profileName;
        _syncingEnvironmentSelection = false;
        SelectEnvironmentScope(createIfMissing: false);
        RefreshEnvironmentDetailProfiles();
        NewProfileName = "";
        Error = "";
    }

    [RelayCommand]
    private void DeleteEnvironmentProfile(string? profileName)
    {
        var environmentName = SelectedEnvironmentDetailName.Trim();
        if (string.IsNullOrWhiteSpace(environmentName) || string.IsNullOrWhiteSpace(profileName))
            return;

        var remove = EnvironmentOptions.FirstOrDefault(option =>
            string.Equals(option.Name, environmentName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.StageLabel, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (remove is null)
            return;

        EnvironmentOptions.Remove(remove);
        _environmentVariableDrafts.Remove(remove.Id);
        if (SelectedEnvironment == remove)
        {
            SelectedEnvironment = null;
            _loadedEnvironmentId = null;
            Variables.Clear();
            OnPropertyChanged(nameof(HasVariables));
        }

        RefreshProfileNameOptions();
        RefreshEnvironmentDetailProfiles();
        if (string.Equals(SelectedEnvironmentName, environmentName, StringComparison.OrdinalIgnoreCase))
        {
            _syncingEnvironmentSelection = true;
            SelectedProfileName = ProfileNameOptions.FirstOrDefault() ?? "";
            _syncingEnvironmentSelection = false;
            SelectEnvironmentScope(createIfMissing: false);
        }
    }

    [RelayCommand]
    private void DeleteEnvironmentFromManager()
        => DeleteEnvironmentByName(SelectedEnvironmentDetailName);

    private void DeleteEnvironmentByName(string? environmentName)
    {
        var remove = environmentName?.Trim();
        if (string.IsNullOrWhiteSpace(remove))
            return;

        foreach (var option in EnvironmentOptions.Where(option => string.Equals(option.Name, remove, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            EnvironmentOptions.Remove(option);
            _environmentVariableDrafts.Remove(option.Id);
        }

        EnvironmentNameOptions.Remove(remove);
        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = EnvironmentNameOptions.FirstOrDefault() ?? "";
        SelectedProfileName = "";
        SelectedEnvironmentDetailName = EnvironmentNameOptions.FirstOrDefault() ?? "";
        _syncingEnvironmentSelection = false;
        RefreshEnvironmentDetailProfiles();
        RefreshEnvironmentNameChips();
        SelectEnvironmentScope(createIfMissing: false);
    }

    [RelayCommand]
    private void AddOrUpdateVariable()
    {
        if (!EnsureSelectedEnvironmentProfile())
            return;

        Error = "";
        try
        {
            var key = string.IsNullOrWhiteSpace(VariableKey) ? throw new InvalidOperationException("Variable key is required.") : VariableKey.Trim();
            if (Variables.Any(row => row != SelectedVariable && string.Equals(row.Key, key, StringComparison.Ordinal)))
                throw new InvalidOperationException("Variable key already exists in this environment.");

            var sourceKind = SelectedVariable?.Entry.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey
                ? ProjectSecretVariableSourceKind.LinkedApiKey
                : ProjectSecretVariableSourceKind.Manual;
            var value = sourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? "" : VariableValue;
            var linkedItemId = sourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedVariable?.Entry.LinkedItemId ?? "" : "";
            var linkedFieldId = sourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedVariable?.Entry.LinkedFieldId ?? "" : "";
            var linkedFieldName = sourceKind == ProjectSecretVariableSourceKind.LinkedApiKey ? SelectedVariable?.Entry.LinkedFieldName ?? "" : "";

            var entry = new ProjectSecretVariableEntry(
                SelectedVariable?.Id ?? Guid.NewGuid().ToString("N"),
                key,
                value,
                VariableIsSecret,
                VariableNotes.Trim(),
                SelectedVariable?.Entry.SortOrder ?? Variables.Count,
                sourceKind,
                linkedItemId,
                linkedFieldId,
                linkedFieldName,
                DateTimeOffset.UtcNow.ToString("O"));

            if (SelectedVariable is null)
                Variables.Add(CreateVariableRow(entry));
            else
                SelectedVariable.Update(entry);

            SaveCurrentVariablesToDraft();
            OnPropertyChanged(nameof(HasVariables));
            ClearVariableForm();
            BuildCompare();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteVariable(ProjectSecretVariableRowVm? variable)
    {
        var remove = variable ?? SelectedVariable;
        if (remove is null)
            return;

        Variables.Remove(remove);
        if (SelectedVariable == remove)
            SelectedVariable = null;
        SaveCurrentVariablesToDraft();
        OnPropertyChanged(nameof(HasVariables));
        ClearVariableForm();
        BuildCompare();
    }

    [RelayCommand]
    private void MoveVariableUp(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        var index = Variables.IndexOf(variable);
        if (index <= 0)
            return;

        Variables.Move(index, index - 1);
        ResequenceVariables();
    }

    [RelayCommand]
    private void MoveVariableDown(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        var index = Variables.IndexOf(variable);
        if (index < 0 || index >= Variables.Count - 1)
            return;

        Variables.Move(index, index + 1);
        ResequenceVariables();
    }

    public void MoveVariable(ProjectSecretVariableRowVm? source, ProjectSecretVariableRowVm? target)
    {
        if (!IsProjectEditing || source is null || target is null || source == target)
            return;

        var oldIndex = Variables.IndexOf(source);
        var newIndex = Variables.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            return;

        Variables.Move(oldIndex, newIndex);
        ResequenceVariables();
    }

    [RelayCommand]
    private void SelectVariable(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        foreach (var row in Variables.Where(row => row != variable))
            row.CancelEdit();

        SelectedVariable = variable;
        variable.BeginEdit();
    }

    [RelayCommand]
    private void CancelVariableEdit(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        variable.CancelEdit();
        if (SelectedVariable == variable)
            SelectedVariable = null;
    }

    [RelayCommand]
    private void SaveVariableEdit(ProjectSecretVariableRowVm? variable)
    {
        if (variable is null)
            return;

        Error = "";
        try
        {
            var key = string.IsNullOrWhiteSpace(variable.EditKey)
                ? throw new InvalidOperationException("Variable key is required.")
                : variable.EditKey.Trim();

            if (Variables.Any(row => row != variable && string.Equals(row.Key, key, StringComparison.Ordinal)))
                throw new InvalidOperationException("Variable key already exists in this environment.");

            variable.Update(variable.BuildEditedEntry() with { Key = key });
            variable.IsEditing = false;
            SelectedVariable = null;
            SaveCurrentVariablesToDraft();
            BuildCompare();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void UseLinkedApiKeyVariableSource()
    {
        VariableSourceKind = ProjectSecretVariableSourceKind.LinkedApiKey;
    }

    [RelayCommand]
    private void UseManualVariableSource()
    {
        VariableSourceKind = ProjectSecretVariableSourceKind.Manual;
    }

    [RelayCommand]
    private void AddLinkedApiKeyVariable(ProjectSecretApiKeyLinkOption? option)
    {
        if (option is null || !EnsureSelectedEnvironmentProfile())
            return;

        Error = "";
        try
        {
            var key = string.IsNullOrWhiteSpace(option.VariableKey)
                ? throw new InvalidOperationException("API Key field label is required.")
                : option.VariableKey.Trim();

            if (Variables.Any(row => string.Equals(row.Key, key, StringComparison.Ordinal)))
                throw new InvalidOperationException("Variable key already exists in this environment.");

            var entry = new ProjectSecretVariableEntry(
                Guid.NewGuid().ToString("N"),
                key,
                "",
                option.IsSensitive,
                "",
                Variables.Count,
                ProjectSecretVariableSourceKind.LinkedApiKey,
                option.ApiKeyItemId,
                option.FieldId,
                option.FieldLabel,
                DateTimeOffset.UtcNow.ToString("O"));

            Variables.Add(CreateVariableRow(entry));
            SaveCurrentVariablesToDraft();
            OnPropertyChanged(nameof(HasVariables));
            ClearVariableForm();
            BuildCompare();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
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
        ImportNewCount = preview.NewRows;
        ImportConflictCount = preview.ConflictRows;
        ImportInvalidCount = preview.InvalidRows;
        ImportPreview = $"{preview.TotalRows} row(s), {preview.ConflictRows} conflict(s), {_lastImportParse.Issues.Count} issue(s)";
    }

    [RelayCommand]
    private async Task ApplyImportAsync()
    {
        if (!EnsureSelectedEnvironmentProfile())
            return;

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
                Variables.Add(CreateVariableRow(entry));
        }

        ImportPreview = $"Imported {_lastImportParse.Variables.Count} variable(s).";
        ImportNewCount = 0;
        ImportConflictCount = 0;
        ImportInvalidCount = 0;
        _root.LogActivity("project_secrets", "Project Secrets .env imported", $"Imported {_lastImportParse.Variables.Count} variables into {ProjectName} / {SelectedEnvironment?.DisplayName}.", "success", affectedItem: ProjectName);
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
        _root.LogActivity("project_secrets", "Project Secrets .env exported", $"Exported {Variables.Count} variables for {ProjectName} / {SelectedEnvironment?.DisplayName} to {Path.GetFileName(ExportPath)}.", "warning", affectedItem: ProjectName);
    }

    [RelayCommand]
    private async Task ExportTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
            await PickExportFileAsync();

        if (string.IsNullOrWhiteSpace(ExportPath))
            return;

        await File.WriteAllTextAsync(ExportPath, EnvFileWriter.WriteTemplate(Variables.Select(row => row.Entry)));
        _root.LogActivity("project_secrets", "Project Secrets .env template exported", $"Exported .env template for {ProjectName} / {SelectedEnvironment?.DisplayName} to {Path.GetFileName(ExportPath)}.", "info", affectedItem: ProjectName);
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

        SaveCurrentVariablesToDraft();
        var root = string.IsNullOrWhiteSpace(ProjectRootPath) ? SelectedProject.ProjectRootPath : ProjectRootPath;
        var variables = EnvironmentOptions
            .SelectMany<ProjectSecretEnvironmentOption, ProjectSecretVariableEntry>(option => _environmentVariableDrafts.TryGetValue(option.Id, out var draftVariables)
                ? draftVariables
                : Array.Empty<ProjectSecretVariableEntry>())
            .ToArray();
        var secrets = variables
            .Where(variable => variable.IsSecret && variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey && !string.IsNullOrWhiteSpace(variable.Value))
            .ToDictionary(variable => variable.Key, variable => variable.Value, StringComparer.Ordinal);

        var scanner = new ProjectSecretFilesystemScanner();
        var result = scanner.Scan(new ProjectSecretScanRequest(SelectedProject.Id, root, variables.Select(variable => variable.Key).ToArray(), secrets));
        ScanFindings.Clear();
        foreach (var finding in result.Findings)
            ScanFindings.Add(new ProjectSecretScanFindingVm(finding));
        NotifyScanGroups();

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
        EnvironmentNameOptions.Clear();
        EnvironmentNameChips.Clear();
        ProfileNameChips.Clear();
        EnvironmentOptions.Clear();
        _environmentVariableDrafts.Clear();
        _loadedEnvironmentId = null;
        ScanFindings.Clear();

        foreach (var environment in project is null
                     ? Array.Empty<ProjectSecretEnvironmentEntry>()
                     : project.Environments.OrderBy(environment => environment.SortOrder).ToArray())
        {
            EnvironmentOptions.Add(new ProjectSecretEnvironmentOption(environment.Id, environment.Name, environment.Kind, ProfileName(environment)));
            if (!EnvironmentNameOptions.Any(name => string.Equals(name, environment.Name, StringComparison.OrdinalIgnoreCase)))
                EnvironmentNameOptions.Add(environment.Name);
            _environmentVariableDrafts[environment.Id] = environment.Variables
                .OrderBy(variable => variable.SortOrder)
                .ToList();
        }

        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = EnvironmentNameOptions.FirstOrDefault() ?? "";
        RefreshProfileNameOptions();
        SelectedProfileName = ProfileNameOptions.FirstOrDefault() ?? "";
        _syncingEnvironmentSelection = false;
        RefreshEnvironmentNameChips();
        RefreshSelectedChips();
        SelectEnvironmentScope(createIfMissing: false);
        SelectFirstEnvironmentProfileIfNeeded();
        if (project?.LastScanResult is { } scan)
        {
            foreach (var finding in scan.Findings)
                ScanFindings.Add(new ProjectSecretScanFindingVm(finding));
        }
        NotifyScanGroups();
    }

    private void SaveCurrentVariablesToDraft()
    {
        if (string.IsNullOrWhiteSpace(_loadedEnvironmentId))
            return;

        _environmentVariableDrafts[_loadedEnvironmentId] = Variables
            .Select(row => row.Entry)
            .ToList();
    }

    private void ResequenceVariables()
    {
        for (var index = 0; index < Variables.Count; index++)
        {
            var row = Variables[index];
            row.Update(row.Entry with { SortOrder = index });
        }

        SaveCurrentVariablesToDraft();
        BuildCompare();
    }

    private void LoadVariablesFromDraft(ProjectSecretEnvironmentOption? environment)
    {
        Variables.Clear();
        if (environment is not null && _environmentVariableDrafts.TryGetValue(environment.Id, out var variables))
        {
            foreach (var variable in variables.OrderBy(variable => variable.SortOrder))
                Variables.Add(CreateVariableRow(variable));
        }

        ClearVariableForm();
        OnPropertyChanged(nameof(HasVariables));
    }

    private ProjectSecretVariableRowVm CreateVariableRow(ProjectSecretVariableEntry entry)
        => new(entry)
        {
            OwnerIsProjectEditing = IsProjectEditing
        };

    private void SelectEnvironmentScope(bool createIfMissing)
    {
        if (_syncingEnvironmentSelection)
            return;

        SaveCurrentVariablesToDraft();
        _syncingEnvironmentSelection = true;
        RefreshProfileNameOptions();
        _syncingEnvironmentSelection = false;
        var environment = FindEnvironmentProfile(SelectedEnvironmentName, SelectedProfileName)
                          ?? ResolveFirstProfileForEnvironment(SelectedEnvironmentName);
        if (environment is null && createIfMissing)
            environment = CreateEnvironmentProfile(SelectedEnvironmentName, SelectedProfileName);

        _syncingEnvironmentSelection = true;
        if (environment is not null)
        {
            SelectedEnvironmentName = environment.Name;
            SelectedProfileName = environment.StageLabel;
            RememberSelectedProfile(environment.Name, environment.StageLabel);
        }

        SelectedEnvironment = environment;
        _syncingEnvironmentSelection = false;
        _loadedEnvironmentId = environment?.Id;
        LoadVariablesFromDraft(environment);
        RefreshSelectedChips();
        BuildCompare();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanEditVariables));
    }

    private void SelectFirstEnvironmentProfileIfNeeded()
    {
        if (SelectedEnvironment is not null || EnvironmentOptions.Count == 0)
            return;

        var preferredProfile = ResolvePreferredProfileName(SelectedEnvironmentName);
        var environment = EnvironmentOptions
            .Where(option => string.IsNullOrWhiteSpace(SelectedEnvironmentName) ||
                             string.Equals(option.Name, SelectedEnvironmentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(option => !string.IsNullOrWhiteSpace(preferredProfile) &&
                                         string.Equals(option.StageLabel, preferredProfile, StringComparison.OrdinalIgnoreCase))
            .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.StageLabel, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? EnvironmentOptions
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.StageLabel, StringComparer.OrdinalIgnoreCase)
                .First();

        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = environment.Name;
        SelectedProfileName = environment.StageLabel;
        RememberSelectedProfile(environment.Name, environment.StageLabel);
        SelectedEnvironment = environment;
        _syncingEnvironmentSelection = false;
        _loadedEnvironmentId = environment.Id;
        LoadVariablesFromDraft(environment);
        BuildCompare();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanEditVariables));
    }

    private bool EnsureSelectedEnvironmentProfile()
    {
        if (string.IsNullOrWhiteSpace(SelectedEnvironmentName) || string.IsNullOrWhiteSpace(SelectedProfileName))
        {
            Error = "Add an environment and profile first.";
            return false;
        }

        var environment = FindEnvironmentProfile(SelectedEnvironmentName, SelectedProfileName)
                          ?? ResolveFirstProfileForEnvironment(SelectedEnvironmentName);
        if (environment is null)
        {
            Error = "Select an existing environment and profile first.";
            return false;
        }

        _syncingEnvironmentSelection = true;
        SelectedEnvironmentName = environment.Name;
        SelectedProfileName = environment.StageLabel;
        SelectedEnvironment = environment;
        _syncingEnvironmentSelection = false;
        RememberSelectedProfile(environment.Name, environment.StageLabel);
        _loadedEnvironmentId = environment.Id;
        RefreshSelectedChips();
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanEditVariables));
        return true;
    }

    private ProjectSecretEnvironmentOption? FindEnvironmentProfile(string? name, string? profile)
    {
        var normalizedName = name?.Trim();
        var normalizedProfile = profile?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedProfile))
            return null;

        return EnvironmentOptions.FirstOrDefault(option =>
            string.Equals(option.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.StageLabel, normalizedProfile, StringComparison.OrdinalIgnoreCase));
    }

    private ProjectSecretEnvironmentOption? ResolveFirstProfileForEnvironment(string? name)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            return null;

        return EnvironmentOptions
            .Where(option => string.Equals(option.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option.StageLabel, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private string ResolvePreferredProfileName(string? environmentName)
    {
        var normalizedEnvironment = environmentName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEnvironment))
            return "";

        if (_sessionSelectedProfilesByEnvironment.TryGetValue(normalizedEnvironment, out var remembered) &&
            EnvironmentOptions.Any(option =>
                string.Equals(option.Name, normalizedEnvironment, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.StageLabel, remembered, StringComparison.OrdinalIgnoreCase)))
        {
            return remembered;
        }

        return EnvironmentOptions
            .Where(option => string.Equals(option.Name, normalizedEnvironment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option.StageLabel, StringComparer.OrdinalIgnoreCase)
            .Select(option => option.StageLabel)
            .FirstOrDefault() ?? "";
    }

    private void RememberSelectedProfile(string? environmentName, string? profileName)
    {
        var normalizedEnvironment = environmentName?.Trim();
        var normalizedProfile = profileName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEnvironment) || string.IsNullOrWhiteSpace(normalizedProfile))
            return;

        _sessionSelectedProfilesByEnvironment[normalizedEnvironment] = normalizedProfile;
    }

    private ProjectSecretEnvironmentOption CreateEnvironmentProfile(string name, string profile)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "Environment" : name.Trim();
        var normalizedProfile = string.IsNullOrWhiteSpace(profile) ? "Default" : profile.Trim();
        if (!EnvironmentNameOptions.Any(option => string.Equals(option, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            EnvironmentNameOptions.Add(normalizedName);
            RefreshEnvironmentNameChips();
        }

        if (!ProfileNameOptions.Any(option => string.Equals(option, normalizedProfile, StringComparison.OrdinalIgnoreCase)))
            ProfileNameOptions.Add(normalizedProfile);
        RefreshProfileNameChips();

        var option = new ProjectSecretEnvironmentOption(Guid.NewGuid().ToString("N"), normalizedName, InferEnvironmentKind(normalizedProfile), normalizedProfile);
        EnvironmentOptions.Add(option);
        _environmentVariableDrafts[option.Id] = [];
        return option;
    }

    private void RefreshProfileNameOptions()
    {
        var previous = SelectedProfileName;
        ProfileNameOptions.Clear();
        foreach (var profileName in EnvironmentOptions
                     .Where(option => string.Equals(option.Name, SelectedEnvironmentName, StringComparison.OrdinalIgnoreCase))
                     .Select(option => option.StageLabel)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(profile => profile, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNameOptions.Add(profileName);
        }

        if (ProfileNameOptions.Count == 0)
        {
            SelectedProfileName = "";
            RefreshProfileNameChips();
            return;
        }

        if (string.IsNullOrWhiteSpace(previous) || !ProfileNameOptions.Any(option => string.Equals(option, previous, StringComparison.OrdinalIgnoreCase)))
            SelectedProfileName = ProfileNameOptions[0];

        RefreshProfileNameChips();
    }

    private void RefreshEnvironmentNameChips()
    {
        EnvironmentNameChips.Clear();
        foreach (var name in EnvironmentNameOptions)
            EnvironmentNameChips.Add(new ProjectSecretNameChipVm(name));

        RefreshSelectedChips();
    }

    private void RefreshProfileNameChips()
    {
        ProfileNameChips.Clear();
        foreach (var name in ProfileNameOptions)
            ProfileNameChips.Add(new ProjectSecretNameChipVm(name));

        RefreshSelectedChips();
    }

    private void RefreshSelectedChips()
    {
        foreach (var chip in EnvironmentNameChips)
            chip.IsSelected = string.Equals(chip.Name, SelectedEnvironmentName, StringComparison.OrdinalIgnoreCase);

        foreach (var chip in ProfileNameChips)
            chip.IsSelected = string.Equals(chip.Name, SelectedProfileName, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshEnvironmentDetailProfiles()
    {
        EnvironmentDetailProfileOptions.Clear();
        if (string.IsNullOrWhiteSpace(SelectedEnvironmentDetailName))
            return;

        foreach (var profileName in EnvironmentOptions
                     .Where(option => string.Equals(option.Name, SelectedEnvironmentDetailName, StringComparison.OrdinalIgnoreCase))
                     .Select(option => option.StageLabel)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(profile => profile, StringComparer.OrdinalIgnoreCase))
        {
            EnvironmentDetailProfileOptions.Add(profileName);
        }
    }

    private string CreateUniqueEnvironmentName(string baseName)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Environment" : baseName.Trim();
        if (!EnvironmentNameOptions.Any(option => string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase)))
            return normalized;

        var suffix = 2;
        while (EnvironmentNameOptions.Any(option => string.Equals(option, $"{normalized} {suffix}", StringComparison.OrdinalIgnoreCase)))
            suffix++;

        return $"{normalized} {suffix}";
    }

    private void NotifyScanGroups()
    {
        OnPropertyChanged(nameof(PlaintextLeakFindings));
        OnPropertyChanged(nameof(MissingReferenceFindings));
        OnPropertyChanged(nameof(UnusedVariableFindings));
        OnPropertyChanged(nameof(ScannerWarningFindings));
        OnPropertyChanged(nameof(LastScanSummary));
    }

    private void BuildCompare()
    {
        SaveCurrentVariablesToDraft();
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
            var variables = _environmentVariableDrafts.TryGetValue(option.Id, out var draftVariables)
                ? draftVariables.Select((variable, variableIndex) => ToInput(variable, variableIndex)).ToArray()
                : existingEnvironment?.Variables.Select((variable, variableIndex) => ToInput(variable, variableIndex)).ToArray() ?? Array.Empty<ProjectSecretVariableInput>();

            return new ProjectSecretEnvironmentInput(
                option.Id,
                option.Name,
                option.Kind,
                variables,
                existingEnvironment?.Notes ?? "",
                index,
                option.StageLabel);
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
                environment.SortOrder,
                environment.ProfileName)).ToArray()
        };

    private static ProjectSecretVariableInput ToInput(ProjectSecretVariableEntry variable, int sortOrder)
        => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, sortOrder, variable.SourceKind, variable.LinkedItemId, variable.LinkedFieldId, variable.LinkedFieldName, variable.LastUpdatedAtUtc);

    private static string ProfileName(ProjectSecretEnvironmentEntry environment)
        => string.IsNullOrWhiteSpace(environment.ProfileName) ? environment.Kind.ToString() : environment.ProfileName.Trim();

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
        ClearLinkedApiKeySelection();
    }

    private void RestoreLinkedApiKeySelection(ProjectSecretVariableEntry variable)
    {
        if (variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey)
        {
            ClearLinkedApiKeySelection();
            return;
        }

        SelectedApiKey = ApiKeyOptions.FirstOrDefault(option => option.ItemId == variable.LinkedItemId);
        SelectedApiKeyField = ApiKeyFieldOptions.FirstOrDefault(option => option.FieldId == variable.LinkedFieldId);
    }

    private void ClearLinkedApiKeySelection()
    {
        SelectedApiKey = null;
        SelectedApiKeyField = null;
        ApiKeyFieldOptions.Clear();
    }

    private static ProjectSecretEnvironmentKind InferEnvironmentKind(string name)
        => Enum.TryParse<ProjectSecretEnvironmentKind>(name, true, out var kind) ? kind : ProjectSecretEnvironmentKind.Development;

    private static string SafeFileName(string value)
        => string.Join("_", (string.IsNullOrWhiteSpace(value) ? "project" : value).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretsViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _root;
    private readonly IProjectSecretService _service;
    private readonly IApiKeyService _apiKeyService;
    private readonly Func<string?, Task> _refreshAllItems;
    private readonly List<ProjectSecretProjectVm> _all = [];
    private readonly List<ApiKeyEntry> _apiKeys = [];
    private readonly ProjectSecretEditSession _session = new();

    public ObservableCollection<ProjectSecretProjectVm> Projects { get; } = [];
    public ProjectSecretVariablesViewModel Variables { get; }
    public ProjectSecretEnvironmentManagerViewModel EnvironmentManager { get; }
    public ProjectSecretImportExportViewModel ImportExport { get; }
    public ProjectSecretCompareViewModel Compare { get; }
    public ProjectSecretScannerViewModel Scanner { get; }
    public ProjectSecretSettingsViewModel Settings { get; }

    [ObservableProperty] private ProjectSecretProjectVm? selectedProject;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool isProjectPickerOpen;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string error = "";

    public bool HasProjects => Projects.Count > 0;
    public bool HasPersistedProject => SelectedProject is not null;
    public bool IsNewProject => SelectedProject is null;
    public bool CanUseDirectOperations => HasPersistedProject && !IsEditing;
    public string HeaderName => SelectedProject?.Name ?? (string.IsNullOrWhiteSpace(Settings.Name) ? "New Project" : Settings.Name);
    public string RootDisplay => string.IsNullOrWhiteSpace(Settings.ProjectRootPath) ? "No project root selected" : Settings.ProjectRootPath;
    public int WarningCount => SelectedProject?.WarningCount ?? 0;
    public string ProjectCountText => $"{Projects.Count} projects";

    public ProjectSecretsViewModel(
        DesktopFeatureServices root,
        IProjectSecretService service,
        IApiKeyService apiKeyService,
        IProjectSecretEnvParser parser,
        IProjectSecretEnvWriter writer,
        IProjectSecretScanner scanner,
        IProjectSecretValueResolver resolver,
        Func<string?, Task> refreshAllItems)
    {
        _root = root; _service = service; _apiKeyService = apiKeyService; _refreshAllItems = refreshAllItems;
        Variables = new(root, resolver, () => _apiKeys) { DraftChanged = ApplyDraft, OpenEnvironmentManagerRequested = OpenEnvironmentManager };
        EnvironmentManager = new() { DraftChanged = ApplyHierarchyDraft };
        ImportExport = new(root, parser, writer, resolver, () => _apiKeys) { PersistRequested = PersistDirectAsync };
        Compare = new(resolver, () => _apiKeys);
        Scanner = new(root, scanner, resolver, () => _apiKeys) { PersistRequested = PersistDirectAsync };
        Settings = new(root) { DeleteRequested = DeleteProjectAsync };
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedProjectChanged(ProjectSecretProjectVm? value)
    {
        if (value is null) return;
        _session.Begin(value.Entry); IsEditing = false; LoadChildren(); IsProjectPickerOpen = false; NotifyHeader();
    }
    partial void OnIsEditingChanged(bool value) { LoadChildren(); NotifyHeader(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_root.VaultPath is null) return;
        IsLoading = true; Error = "";
        try
        {
            var selectedId = SelectedProject?.Id;
            _all.Clear();
            _all.AddRange((await _service.ListAsync(_root.VaultPath, _root.VaultKey)).OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).Select(project => new ProjectSecretProjectVm(project)));
            await RefreshApiKeysAsync();
            ApplyFilter();
            var selected = _all.FirstOrDefault(project => project.Id == selectedId) ?? _all.FirstOrDefault();
            if (selected is null) BeginNew(); else SelectedProject = selected;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand] private void ToggleProjectPicker() => IsProjectPickerOpen = !IsProjectPickerOpen;
    [RelayCommand] private void SelectProject(ProjectSecretProjectVm? project) { if (!IsEditing && project is not null) SelectedProject = project; }
    [RelayCommand] public void AddProject() { if (!IsEditing) BeginNew(); }
    [RelayCommand] private void EditProject() { if (SelectedProject is not null) IsEditing = true; }

    [RelayCommand]
    private void CancelEdit()
    {
        if (_session.IsNew) { var first = _all.FirstOrDefault(); if (first is null) BeginNew(); else SelectedProject = first; return; }
        _session.Restore(); IsEditing = false; LoadChildren();
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (_root.VaultPath is null) return;
        Error = "";
        try
        {
            var input = Settings.Apply(_session.Draft);
            var saved = _session.Original is null
                ? await _service.AddAsync(_root.VaultPath, _root.VaultKey, input)
                : await _service.UpdateAsync(_root.VaultPath, _root.VaultKey, _session.Original.Id, _session.Original.CreatedAtUtc, input);
            _root.LogActivity("project_secrets", _session.Original is null ? "Project Secrets project created" : "Project Secrets project updated", $"{(_session.Original is null ? "Created" : "Updated")} Project Secrets project {saved.Name}.", "success", affectedItem: saved.Name);
            await ReloadAndSelectAsync(saved.Id); await _refreshAllItems(saved.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (_all.Count == 0) await LoadAsync();
        var project = _all.FirstOrDefault(item => item.Id == itemId);
        if (project is null) return false;
        SelectedProject = project; return true;
    }

    public async Task RefreshApiKeysAsync()
    {
        if (_root.VaultPath is null)
            return;

        try
        {
            var apiKeys = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
            _apiKeys.Clear();
            _apiKeys.AddRange(apiKeys);
            Variables.RefreshApiKeys();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public override void RefreshLocalization() { }

    private void BeginNew()
    {
        SelectedProject = null; _session.BeginNew(); IsEditing = true; IsProjectPickerOpen = false; LoadChildren(); NotifyHeader();
    }

    private void OpenEnvironmentManager() { if (IsEditing) EnvironmentManager.Open(_session.Draft); }

    private void ApplyDraft(ProjectSecretInput draft)
    {
        _session.Replace(draft); Compare.Load(draft); EnvironmentManager.Load(draft); NotifyHeader();
    }

    private void ApplyHierarchyDraft(ProjectSecretInput draft)
    {
        _session.Replace(draft); Variables.Load(draft, IsEditing); Compare.Load(draft); NotifyHeader();
    }

    private void LoadChildren()
    {
        var draft = _session.Draft;
        Settings.Load(draft, IsEditing, HasPersistedProject);
        Variables.Load(draft, IsEditing);
        EnvironmentManager.Load(draft);
        Compare.Load(draft);
        ImportExport.Load(draft, CanUseDirectOperations);
        Scanner.Load(SelectedProject?.Id ?? "", draft, CanUseDirectOperations);
    }

    private async Task PersistDirectAsync(ProjectSecretInput input)
    {
        if (_root.VaultPath is null || SelectedProject is null || IsEditing) return;
        var saved = await _service.UpdateAsync(_root.VaultPath, _root.VaultKey, SelectedProject.Id, SelectedProject.Entry.CreatedAtUtc, input);
        await ReloadAndSelectAsync(saved.Id); await _refreshAllItems(saved.Id);
    }

    private async Task DeleteProjectAsync()
    {
        if (_root.VaultPath is null || SelectedProject is null) return;
        if (!await _root.ConfirmAsync("Delete Project Secrets project", $"Delete {SelectedProject.Name}? This removes its encrypted environments, profiles, and variables.", "Delete", destructive: true)) return;
        var id = SelectedProject.Id; var name = SelectedProject.Name;
        await _service.DeleteAsync(_root.VaultPath, id);
        _root.LogActivity("project_secrets", "Project Secrets project deleted", $"Deleted Project Secrets project {name}.", "warning", affectedItem: name);
        await LoadAsync(); await _refreshAllItems(null);
    }

    private async Task ReloadAndSelectAsync(string id)
    {
        await LoadAsync();
        var project = _all.FirstOrDefault(item => item.Id == id);
        if (project is not null) SelectedProject = project;
        IsEditing = false;
    }

    private void ApplyFilter()
    {
        Projects.Clear(); var text = SearchText.Trim();
        foreach (var project in _all.Where(project => text.Length == 0 || project.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || project.Description.Contains(text, StringComparison.OrdinalIgnoreCase))) Projects.Add(project);
        OnPropertyChanged(nameof(HasProjects)); OnPropertyChanged(nameof(ProjectCountText));
    }

    private void NotifyHeader()
    {
        OnPropertyChanged(nameof(HasPersistedProject)); OnPropertyChanged(nameof(IsNewProject)); OnPropertyChanged(nameof(CanUseDirectOperations));
        OnPropertyChanged(nameof(HeaderName)); OnPropertyChanged(nameof(RootDisplay)); OnPropertyChanged(nameof(WarningCount));
    }
}

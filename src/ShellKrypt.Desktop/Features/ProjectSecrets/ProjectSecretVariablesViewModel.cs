using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretVariablesViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _root;
    private readonly IProjectSecretValueResolver _resolver;
    private readonly Func<IReadOnlyList<ApiKeyEntry>> _apiKeys;
    private ProjectSecretInput _draft = ProjectSecretEditSession.Empty();
    public Action<ProjectSecretInput>? DraftChanged { get; set; }
    public Action? OpenEnvironmentManagerRequested { get; set; }

    public ObservableCollection<ProjectSecretEnvironmentOption> Environments { get; } = [];
    public ObservableCollection<ProjectSecretProfileOption> Profiles { get; } = [];
    public ObservableCollection<ProjectSecretVariableVm> Variables { get; } = [];
    public ProjectSecretVariableEditorViewModel Editor { get; }

    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private ProjectSecretProfileOption? selectedProfile;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string error = "";

    public bool HasEnvironment => SelectedEnvironment is not null;
    public bool HasProfile => SelectedProfile is not null;
    public bool HasVariables => Variables.Count > 0;
    public bool CanAddVariable => IsEditing && HasProfile;
    public bool ShowNoProfile => HasEnvironment && !HasProfile;
    public bool ShowNoVariables => HasProfile && !HasVariables;

    public ProjectSecretVariablesViewModel(DesktopFeatureServices root, IProjectSecretValueResolver resolver, Func<IReadOnlyList<ApiKeyEntry>> apiKeys)
    {
        _root = root;
        _resolver = resolver;
        _apiKeys = apiKeys;
        Editor = new ProjectSecretVariableEditorViewModel(root) { SaveRequested = SaveVariableAsync };
    }

    public void Load(ProjectSecretInput draft, bool editing)
    {
        ResetReveal();
        _draft = draft;
        IsEditing = editing;
        Editor.SetApiKeys(_apiKeys());
        RefreshHierarchy(SelectedEnvironment?.Id, SelectedProfile?.Id);
    }

    public void RefreshApiKeys()
    {
        Editor.SetApiKeys(_apiKeys());
        RefreshVariables();
    }

    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        ResetReveal();
        foreach (var option in Environments) option.IsSelected = option == value;
        Profiles.Clear();
        var environment = value is null ? null : _draft.Environments.FirstOrDefault(item => item.Id == value.Id);
        if (environment is not null)
            foreach (var profile in environment.Profiles.OrderBy(item => item.SortOrder))
                Profiles.Add(new ProjectSecretProfileOption(environment.Id, profile.Id, profile.Name));
        SelectedProfile = Profiles.FirstOrDefault();
        NotifyState();
    }

    partial void OnSelectedProfileChanged(ProjectSecretProfileOption? value)
    {
        ResetReveal();
        foreach (var option in Profiles) option.IsSelected = option == value;
        RefreshVariables();
        NotifyState();
    }

    [RelayCommand] private void SelectEnvironment(ProjectSecretEnvironmentOption? value) { if (value is not null) SelectedEnvironment = value; }
    [RelayCommand] private void SelectProfile(ProjectSecretProfileOption? value) { if (value is not null) SelectedProfile = value; }
    [RelayCommand] private void ManageEnvironments() => OpenEnvironmentManagerRequested?.Invoke();
    [RelayCommand] private void AddVariable() { if (CanAddVariable) Editor.OpenAdd(); }
    [RelayCommand] private void ReferenceApiKey() { if (CanAddVariable) Editor.OpenAdd(ProjectSecretVariableSourceKind.ReferencedApiKey); }
    [RelayCommand] private void ImportApiKeyCopy() { if (CanAddVariable) Editor.OpenAdd(ProjectSecretVariableSourceKind.ImportedApiKey); }
    [RelayCommand] private void EditVariable(ProjectSecretVariableVm? row) { if (IsEditing && row is not null) Editor.OpenEdit(row.Entry); }
    [RelayCommand] private void ToggleVariable(ProjectSecretVariableVm? row) { if (row is not null) row.IsValueRevealed = !row.IsValueRevealed; }

    [RelayCommand]
    private async Task CopyVariableAsync(ProjectSecretVariableVm? row)
    {
        if (row is null) return;
        var value = Resolve(row.Entry) ?? "";
        await _root.CopyToClipboardAsync(value);
        _root.LogActivity("project_secrets", "Project variable copied", $"Copied {row.Key}.", "info", affectedItem: row.Key);
    }

    [RelayCommand]
    private async Task DeleteVariableAsync(ProjectSecretVariableVm? row)
    {
        if (!IsEditing || row is null || SelectedEnvironment is null || SelectedProfile is null) return;
        if (!await _root.ConfirmAsync("Delete variable", $"Delete {row.Key}?", "Delete", destructive: true)) return;
        ReplaceVariables(CurrentProfile()!.Variables.Where(variable => variable.Id != row.Id).Select((variable, index) => variable with { SortOrder = index }).ToArray());
    }

    public void MoveVariable(ProjectSecretVariableVm? source, ProjectSecretVariableVm? target)
    {
        if (!IsEditing || source is null || target is null || source == target) return;
        var variables = CurrentProfile()!.Variables.ToList();
        var oldIndex = variables.FindIndex(item => item.Id == source.Id);
        var newIndex = variables.FindIndex(item => item.Id == target.Id);
        if (oldIndex < 0 || newIndex < 0) return;
        var item = variables[oldIndex]; variables.RemoveAt(oldIndex); variables.Insert(newIndex, item);
        ReplaceVariables(variables.Select((variable, index) => variable with { SortOrder = index }).ToArray());
    }

    private Task<bool> SaveVariableAsync(ProjectSecretVariableEntry entry)
    {
        var profile = CurrentProfile();
        if (profile is null) return Task.FromResult(false);
        if (profile.Variables.Any(variable => variable.Id != entry.Id && string.Equals(variable.Key, entry.Key, StringComparison.OrdinalIgnoreCase)))
        {
            Editor.Error = "Variable key already exists in this profile.";
            return Task.FromResult(false);
        }
        var input = new ProjectSecretVariableInput(entry.Id, entry.Key, entry.Value, entry.IsSecret, entry.Notes, entry.SortOrder, entry.SourceKind, entry.ReferencedItemId, entry.ReferencedFieldId, entry.ReferencedFieldName, entry.LastUpdatedAtUtc);
        var existing = profile.Variables.FirstOrDefault(variable => variable.Id == entry.Id);
        var variables = existing is null ? profile.Variables.Append(input with { SortOrder = profile.Variables.Count }).ToArray() : profile.Variables.Select(variable => variable.Id == entry.Id ? input with { SortOrder = variable.SortOrder } : variable).ToArray();
        ReplaceVariables(variables);
        return Task.FromResult(true);
    }

    private void ReplaceVariables(IReadOnlyList<ProjectSecretVariableInput> variables)
    {
        if (SelectedEnvironment is null || SelectedProfile is null) return;
        _draft = _draft with { Environments = _draft.Environments.Select(environment => environment.Id != SelectedEnvironment.Id ? environment : environment with { Profiles = environment.Profiles.Select(profile => profile.Id != SelectedProfile.Id ? profile : profile with { Variables = variables }).ToArray() }).ToArray() };
        DraftChanged?.Invoke(_draft);
        RefreshVariables();
    }

    private ProjectSecretProfileInput? CurrentProfile()
        => SelectedEnvironment is null || SelectedProfile is null ? null : _draft.Environments.FirstOrDefault(environment => environment.Id == SelectedEnvironment.Id)?.Profiles.FirstOrDefault(profile => profile.Id == SelectedProfile.Id);

    private string? Resolve(ProjectSecretVariableEntry variable) => _resolver.Resolve(variable, _apiKeys());

    private void RefreshHierarchy(string? environmentId, string? profileId)
    {
        Environments.Clear();
        foreach (var environment in _draft.Environments.OrderBy(item => item.SortOrder))
            Environments.Add(new ProjectSecretEnvironmentOption(environment.Id, environment.Name));
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == environmentId) ?? Environments.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profileId) ?? Profiles.FirstOrDefault();
        NotifyState();
    }

    private void RefreshVariables()
    {
        Variables.Clear();
        var profile = CurrentProfile();
        if (profile is not null)
            foreach (var variable in profile.Variables.OrderBy(item => item.SortOrder).Select(ToEntry))
                Variables.Add(new ProjectSecretVariableVm(variable, Resolve));
        OnPropertyChanged(nameof(HasVariables));
        OnPropertyChanged(nameof(ShowNoVariables));
    }

    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariableInput variable)
        => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, variable.SortOrder, variable.SourceKind, variable.ReferencedItemId, variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc);

    private void ResetReveal() { foreach (var variable in Variables) variable.ResetReveal(); Editor.CloseCommand.Execute(null); }
    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasEnvironment));
        OnPropertyChanged(nameof(HasProfile));
        OnPropertyChanged(nameof(CanAddVariable));
        OnPropertyChanged(nameof(ShowNoProfile));
        OnPropertyChanged(nameof(ShowNoVariables));
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretEnvironmentManagerViewModel : ViewModelBase
{
    private ProjectSecretInput _draft = ProjectSecretEditSession.Empty();
    private ProjectSecretProfileOption? _pendingDeleteProfile;
    private bool _pendingDeleteEnvironment;
    public Action<ProjectSecretInput>? DraftChanged { get; set; }
    public ObservableCollection<ProjectSecretEnvironmentOption> Environments { get; } = [];
    public ObservableCollection<ProjectSecretProfileOption> Profiles { get; } = [];

    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private string newEnvironmentName = "";
    [ObservableProperty] private string newProfileName = "";
    [ObservableProperty] private string environmentName = "";
    [ObservableProperty] private ProjectSecretProfileOption? editingProfile;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isDeleteConfirmationOpen;
    [ObservableProperty] private string deleteConfirmationTitle = "";
    [ObservableProperty] private string deleteConfirmationMessage = "";

    public bool HasEnvironment => SelectedEnvironment is not null;
    public bool IsRenamingProfile => EditingProfile is not null;

    public void Open(ProjectSecretInput draft)
    {
        _draft = draft;
        Error = "";
        Refresh(SelectedEnvironment?.Id);
        IsOpen = true;
    }

    public void Load(ProjectSecretInput draft)
    {
        _draft = draft;
        Refresh(SelectedEnvironment?.Id);
    }

    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        foreach (var option in Environments) option.IsSelected = option == value;
        Profiles.Clear();
        var environment = value is null ? null : _draft.Environments.FirstOrDefault(item => item.Id == value.Id);
        EnvironmentName = environment?.Name ?? "";
        if (environment is not null)
            foreach (var profile in environment.Profiles.OrderBy(profile => profile.SortOrder))
                Profiles.Add(new ProjectSecretProfileOption(environment.Id, profile.Id, profile.Name));
        OnPropertyChanged(nameof(HasEnvironment));
    }

    [RelayCommand] private void Close()
    {
        CancelDelete();
        IsOpen = false;
    }
    [RelayCommand] private void SelectEnvironment(ProjectSecretEnvironmentOption? option)
    {
        if (option is not null)
            SelectedEnvironment = option;
    }

    [RelayCommand]
    private void AddEnvironment()
    {
        Error = "";
        var name = NewEnvironmentName.Trim();
        if (name.Length == 0) { Error = "Environment name is required."; return; }
        if (_draft.Environments.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { Error = "Environment name already exists."; return; }
        var environment = new ProjectSecretEnvironmentInput(Guid.NewGuid().ToString("N"), name, "", _draft.Environments.Count, Array.Empty<ProjectSecretProfileInput>());
        Apply(_draft with { Environments = _draft.Environments.Append(environment).ToArray() }, environment.Id);
        NewEnvironmentName = "";
    }

    [RelayCommand]
    private void RenameEnvironment()
    {
        if (SelectedEnvironment is null) return;
        Error = "";
        var name = EnvironmentName.Trim();
        if (name.Length == 0) { Error = "Environment name is required."; return; }
        if (_draft.Environments.Any(item => item.Id != SelectedEnvironment.Id && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { Error = "Environment name already exists."; return; }
        Apply(_draft with { Environments = _draft.Environments.Select(item => item.Id == SelectedEnvironment.Id ? item with { Name = name } : item).ToArray() }, SelectedEnvironment.Id);
    }

    [RelayCommand]
    private void AddProfile()
    {
        if (SelectedEnvironment is null) return;
        Error = "";
        var name = NewProfileName.Trim();
        var environment = _draft.Environments.First(item => item.Id == SelectedEnvironment.Id);
        if (name.Length == 0) { Error = "Profile name is required."; return; }
        if (environment.Profiles.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { Error = "Profile name already exists in this environment."; return; }
        var profile = new ProjectSecretProfileInput(Guid.NewGuid().ToString("N"), name, environment.Profiles.Count, Array.Empty<ProjectSecretVariableInput>());
        Apply(_draft with { Environments = _draft.Environments.Select(item => item.Id == environment.Id ? item with { Profiles = item.Profiles.Append(profile).ToArray() } : item).ToArray() }, environment.Id);
        NewProfileName = "";
    }

    [RelayCommand]
    private void BeginRenameProfile(ProjectSecretProfileOption? option)
    {
        if (option is null) return;
        EditingProfile = option;
        NewProfileName = option.Name;
    }

    [RelayCommand]
    private void CancelRenameProfile()
    {
        EditingProfile = null;
        NewProfileName = "";
    }

    [RelayCommand]
    private void SaveProfileName()
    {
        if (EditingProfile is null) return;
        var option = EditingProfile;
        var name = NewProfileName.Trim();
        var environment = _draft.Environments.First(item => item.Id == option.EnvironmentId);
        if (name.Length == 0) { Error = "Profile name is required."; return; }
        if (environment.Profiles.Any(item => item.Id != option.Id && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { Error = "Profile name already exists in this environment."; return; }
        Apply(_draft with { Environments = _draft.Environments.Select(item => item.Id == environment.Id ? item with { Profiles = item.Profiles.Select(profile => profile.Id == option.Id ? profile with { Name = name } : profile).ToArray() } : item).ToArray() }, environment.Id);
        EditingProfile = null;
        NewProfileName = "";
    }

    partial void OnEditingProfileChanged(ProjectSecretProfileOption? value) =>
        OnPropertyChanged(nameof(IsRenamingProfile));

    [RelayCommand]
    private void DeleteProfile(ProjectSecretProfileOption? option)
    {
        if (option is null) return;
        _pendingDeleteProfile = option;
        _pendingDeleteEnvironment = false;
        DeleteConfirmationTitle = $"Delete {option.Name}?";
        DeleteConfirmationMessage = "This removes the profile, all variables inside it, and its latest scan result.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void DeleteEnvironment()
    {
        if (SelectedEnvironment is null) return;
        _pendingDeleteProfile = null;
        _pendingDeleteEnvironment = true;
        DeleteConfirmationTitle = $"Delete {SelectedEnvironment.Name}?";
        DeleteConfirmationMessage = "This removes the environment, every profile and variable inside it, and all related scan results.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (_pendingDeleteProfile is { } profile)
        {
            Apply(_draft with
            {
                Environments = _draft.Environments.Select(item => item.Id == profile.EnvironmentId
                    ? item with { Profiles = item.Profiles.Where(candidate => candidate.Id != profile.Id).Select((candidate, index) => candidate with { SortOrder = index }).ToArray() }
                    : item).ToArray(),
                ScanResults = _draft.ScanResults.Where(result => result.ProfileId != profile.Id).ToArray()
            }, profile.EnvironmentId);
        }
        else if (_pendingDeleteEnvironment && SelectedEnvironment is { } environment)
        {
            Apply(_draft with
            {
                Environments = _draft.Environments.Where(item => item.Id != environment.Id).Select((item, index) => item with { SortOrder = index }).ToArray(),
                ScanResults = _draft.ScanResults.Where(result => result.EnvironmentId != environment.Id).ToArray()
            }, null);
        }

        CancelDelete();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        _pendingDeleteProfile = null;
        _pendingDeleteEnvironment = false;
        DeleteConfirmationTitle = "";
        DeleteConfirmationMessage = "";
        IsDeleteConfirmationOpen = false;
    }

    [RelayCommand] private void MoveEnvironmentUp(ProjectSecretEnvironmentOption? option) => MoveEnvironment(option, -1);
    [RelayCommand] private void MoveEnvironmentDown(ProjectSecretEnvironmentOption? option) => MoveEnvironment(option, 1);
    [RelayCommand] private void MoveProfileUp(ProjectSecretProfileOption? option) => MoveProfile(option, -1);
    [RelayCommand] private void MoveProfileDown(ProjectSecretProfileOption? option) => MoveProfile(option, 1);

    private void MoveEnvironment(ProjectSecretEnvironmentOption? option, int offset)
    {
        if (option is null) return;
        var items = _draft.Environments.OrderBy(item => item.SortOrder).ToList();
        var current = items.FindIndex(item => item.Id == option.Id);
        var target = current + offset;
        if (current < 0 || target < 0 || target >= items.Count) return;
        (items[current], items[target]) = (items[target], items[current]);
        Apply(_draft with { Environments = items.Select((item, index) => item with { SortOrder = index }).ToArray() }, option.Id);
    }

    private void MoveProfile(ProjectSecretProfileOption? option, int offset)
    {
        if (option is null) return;
        var environment = _draft.Environments.First(item => item.Id == option.EnvironmentId);
        var profiles = environment.Profiles.OrderBy(item => item.SortOrder).ToList();
        var current = profiles.FindIndex(item => item.Id == option.Id);
        var target = current + offset;
        if (current < 0 || target < 0 || target >= profiles.Count) return;
        (profiles[current], profiles[target]) = (profiles[target], profiles[current]);
        Apply(_draft with { Environments = _draft.Environments.Select(item => item.Id == environment.Id ? item with { Profiles = profiles.Select((profile, index) => profile with { SortOrder = index }).ToArray() } : item).ToArray() }, environment.Id);
    }

    private void Apply(ProjectSecretInput draft, string? preferredId)
    {
        _draft = draft;
        DraftChanged?.Invoke(draft);
        Refresh(preferredId);
    }

    private void Refresh(string? preferredId)
    {
        Environments.Clear();
        foreach (var environment in _draft.Environments.OrderBy(item => item.SortOrder))
            Environments.Add(new ProjectSecretEnvironmentOption(environment.Id, environment.Name));
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == preferredId) ?? Environments.FirstOrDefault();
    }
}

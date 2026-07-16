using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretSettingsViewModel(DesktopFeatureServices root) : ViewModelBase
{
    public Func<Task>? DeleteRequested { get; set; }

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private string projectRootPath = "";
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool hasPersistedProject;

    public void Load(ProjectSecretInput input, bool editing, bool persisted)
    {
        Name = input.Name;
        Description = input.Description;
        Notes = input.Notes;
        ProjectRootPath = input.ProjectRootPath ?? "";
        IsEditing = editing;
        HasPersistedProject = persisted;
    }

    public ProjectSecretInput Apply(ProjectSecretInput input)
        => input with
        {
            Name = Name.Trim(), Description = Description.Trim(), Notes = Notes.Trim(),
            ProjectRootPath = string.IsNullOrWhiteSpace(ProjectRootPath) ? null : ProjectRootPath.Trim()
        };

    [RelayCommand]
    private async Task PickProjectRootAsync()
    {
        var path = await root.PickFolderAsync("Choose project root");
        if (!string.IsNullOrWhiteSpace(path))
            ProjectRootPath = path;
    }

    [RelayCommand]
    private Task DeleteProjectAsync() => DeleteRequested?.Invoke() ?? Task.CompletedTask;
}

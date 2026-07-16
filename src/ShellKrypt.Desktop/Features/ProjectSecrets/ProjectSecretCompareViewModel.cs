using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretCompareViewModel(IProjectSecretValueResolver resolver, Func<IReadOnlyList<ApiKeyEntry>> apiKeys) : ViewModelBase
{
    private ProjectSecretInput _project = ProjectSecretEditSession.Empty();
    public ObservableCollection<ProjectSecretEnvironmentOption> Environments { get; } = [];
    public ObservableCollection<ProjectSecretCompareRowVm> Rows { get; } = [];
    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    public bool HasRows => Rows.Count > 0;
    public bool HasEnvironment => SelectedEnvironment is not null;
    public bool ShowEmpty => HasEnvironment && !HasRows;

    public void Load(ProjectSecretInput project)
    {
        _project = project; Environments.Clear();
        foreach (var environment in project.Environments.OrderBy(item => item.SortOrder)) Environments.Add(new(environment.Id, environment.Name));
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == SelectedEnvironment?.Id) ?? Environments.FirstOrDefault();
        Build();
    }
    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        foreach (var option in Environments) option.IsSelected = option == value;
        Build();
    }
    [RelayCommand] private void SelectEnvironment(ProjectSecretEnvironmentOption? value) { if (value is not null) SelectedEnvironment = value; }
    private void Build()
    {
        Rows.Clear();
        var input = SelectedEnvironment is null ? null : _project.Environments.FirstOrDefault(item => item.Id == SelectedEnvironment.Id);
        if (input is null) { NotifyState(); return; }
        var environment = new ProjectSecretEnvironmentEntry(input.Id, input.Name, input.Notes, input.SortOrder, input.Profiles.Select(profile => new ProjectSecretProfileEntry(profile.Id, profile.Name, profile.SortOrder, profile.Variables.Select(ToEntry).ToArray())).ToArray());
        foreach (var row in ProjectSecretComparer.Compare(environment, valueResolver: variable => resolver.Resolve(variable, apiKeys())).Rows) Rows.Add(new(row.VariableKey, row.Cells));
        NotifyState();
    }
    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasEnvironment));
        OnPropertyChanged(nameof(ShowEmpty));
    }
    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariableInput variable) => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, variable.SortOrder, variable.SourceKind, variable.ReferencedItemId, variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc);
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretScannerViewModel : ViewModelBase
{
    private readonly ProjectSecretsRuntime _root;
    private readonly IProjectSecretScanner _scanner;
    private readonly IProjectSecretValueResolver _resolver;
    private readonly Func<IReadOnlyList<ApiKeyEntry>> _apiKeys;
    private ProjectSecretInput _project = ProjectSecretEditSession.Empty();
    private string _projectId = "";
    private CancellationTokenSource? _scanCts;
    public Func<ProjectSecretInput, Task>? PersistRequested { get; set; }
    public ObservableCollection<ProjectSecretEnvironmentOption> Environments { get; } = [];
    public ObservableCollection<ProjectSecretProfileOption> Profiles { get; } = [];
    public ObservableCollection<ProjectSecretScanFindingVm> Findings { get; } = [];

    [ObservableProperty] private ProjectSecretEnvironmentOption? selectedEnvironment;
    [ObservableProperty] private ProjectSecretProfileOption? selectedProfile;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string error = "";

    public bool HasTarget => SelectedProfile is not null;
    public bool CanScan => IsEnabled && HasTarget && !IsScanning && !string.IsNullOrWhiteSpace(_project.ProjectRootPath);
    public bool HasFindings => Findings.Count > 0;
    public bool ShowEmpty => HasTarget && !HasFindings && !IsScanning;
    public bool ShowUnavailable => !IsEnabled;
    public string ProjectRootDisplay => string.IsNullOrWhiteSpace(_project.ProjectRootPath) ? "No project root selected" : _project.ProjectRootPath;

    public ProjectSecretScannerViewModel(ProjectSecretsRuntime root, IProjectSecretScanner scanner, IProjectSecretValueResolver resolver, Func<IReadOnlyList<ApiKeyEntry>> apiKeys)
    { _root = root; _scanner = scanner; _resolver = resolver; _apiKeys = apiKeys; }

    public void Load(string projectId, ProjectSecretInput project, bool enabled)
    {
        CancelScan(); _projectId = projectId; _project = project; IsEnabled = enabled;
        Environments.Clear(); foreach (var environment in project.Environments.OrderBy(item => item.SortOrder)) Environments.Add(new(environment.Id, environment.Name));
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == SelectedEnvironment?.Id) ?? Environments.FirstOrDefault();
        RefreshLastResult(); NotifyState();
        OnPropertyChanged(nameof(ProjectRootDisplay));
    }
    partial void OnSelectedEnvironmentChanged(ProjectSecretEnvironmentOption? value)
    {
        foreach (var option in Environments) option.IsSelected = option == value;
        Profiles.Clear(); var environment = value is null ? null : _project.Environments.FirstOrDefault(item => item.Id == value.Id);
        if (environment is not null) foreach (var profile in environment.Profiles.OrderBy(item => item.SortOrder)) Profiles.Add(new(environment.Id, profile.Id, profile.Name));
        SelectedProfile = Profiles.FirstOrDefault(); RefreshLastResult(); NotifyState();
    }
    partial void OnSelectedProfileChanged(ProjectSecretProfileOption? value)
    {
        foreach (var option in Profiles) option.IsSelected = option == value;
        RefreshLastResult(); NotifyState();
    }
    partial void OnIsEnabledChanged(bool value) => NotifyState();
    partial void OnIsScanningChanged(bool value) => NotifyState();

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!CanScan || SelectedEnvironment is null || SelectedProfile is null) return;
        Error = Status = ""; Findings.Clear(); IsScanning = true; _scanCts = new();
        try
        {
            var profile = CurrentProfile()!;
            var entries = profile.Variables.Select(ToEntry).ToArray();
            var secrets = entries.Where(variable => variable.IsSecret).Select(variable => (variable.Key, Value: _resolver.Resolve(variable, _apiKeys()) ?? "")).Where(pair => pair.Value.Length > 0).GroupBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
            var result = await _scanner.ScanAsync(new ProjectSecretScanRequest(_projectId, SelectedEnvironment.Id, SelectedProfile.Id, _project.ProjectRootPath!, entries.Select(variable => variable.Key).ToArray(), secrets), _scanCts.Token);
            foreach (var finding in result.Findings) Findings.Add(new(finding));
            var results = _project.ScanResults.Where(item => item.ProfileId != result.ProfileId).Append(result).ToArray();
            var updated = _project with { ScanResults = results };
            if (PersistRequested is not null) await PersistRequested(updated);
            _project = updated;
            Status = $"{result.FilesScanned} files scanned, {result.Findings.Count} findings.";
            _root.LogActivity("project_secrets", "Project folder scanned", $"Scanned {_project.Name}: {result.FilesScanned} files, {result.Findings.Count} findings.", "info", affectedItem: _project.Name);
            OnPropertyChanged(nameof(HasFindings)); OnPropertyChanged(nameof(ShowEmpty));
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception ex) { Error = ex.Message; }
        finally { _scanCts?.Dispose(); _scanCts = null; IsScanning = false; }
    }

    [RelayCommand] private void CancelScan() => _scanCts?.Cancel();

    private ProjectSecretProfileInput? CurrentProfile() => SelectedEnvironment is null || SelectedProfile is null ? null : _project.Environments.FirstOrDefault(environment => environment.Id == SelectedEnvironment.Id)?.Profiles.FirstOrDefault(profile => profile.Id == SelectedProfile.Id);
    private void RefreshLastResult()
    {
        Findings.Clear();
        if (SelectedProfile is not null)
        {
            var result = _project.ScanResults.FirstOrDefault(item => item.ProfileId == SelectedProfile.Id);
            if (result is not null) foreach (var finding in result.Findings) Findings.Add(new(finding));
        }
        OnPropertyChanged(nameof(HasFindings)); OnPropertyChanged(nameof(ShowEmpty));
    }
    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariableInput variable) => new(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, variable.SortOrder, variable.SourceKind, variable.ReferencedItemId, variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc);
    private void NotifyState() { OnPropertyChanged(nameof(HasTarget)); OnPropertyChanged(nameof(CanScan)); OnPropertyChanged(nameof(ShowEmpty)); OnPropertyChanged(nameof(ShowUnavailable)); }
}

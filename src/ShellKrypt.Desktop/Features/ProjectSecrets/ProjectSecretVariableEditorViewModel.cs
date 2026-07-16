using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ProjectSecrets;

public partial class ProjectSecretVariableEditorViewModel(ProjectSecretsRuntime root) : ViewModelBase
{
    private ProjectSecretVariableEntry? _original;
    public Func<ProjectSecretVariableEntry, Task<bool>>? SaveRequested { get; set; }
    public ObservableCollection<ProjectSecretApiKeyFieldOption> ApiKeyFields { get; } = [];

    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private string key = "";
    [ObservableProperty] private string value = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private bool isSecret = true;
    [ObservableProperty] private bool isValueVisible;
    [ObservableProperty] private ProjectSecretVariableSourceKind sourceKind = ProjectSecretVariableSourceKind.Manual;
    [ObservableProperty] private ProjectSecretApiKeyFieldOption? selectedApiKeyField;
    [ObservableProperty] private string error = "";

    public bool IsEdit => _original is not null;
    public bool IsManual => SourceKind is ProjectSecretVariableSourceKind.Manual or ProjectSecretVariableSourceKind.ImportedEnvFile;
    public bool IsApiKeySource => SourceKind is ProjectSecretVariableSourceKind.ReferencedApiKey or ProjectSecretVariableSourceKind.ImportedApiKey;
    public bool IsManualSource => SourceKind == ProjectSecretVariableSourceKind.Manual;
    public bool IsReferenceSource => SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey;
    public bool IsImportedCopySource => SourceKind == ProjectSecretVariableSourceKind.ImportedApiKey;
    public string Title => IsEdit ? "Edit variable" : "Add variable";

    partial void OnSourceKindChanged(ProjectSecretVariableSourceKind value)
    {
        OnPropertyChanged(nameof(IsManual));
        OnPropertyChanged(nameof(IsApiKeySource));
        OnPropertyChanged(nameof(IsManualSource));
        OnPropertyChanged(nameof(IsReferenceSource));
        OnPropertyChanged(nameof(IsImportedCopySource));
    }

    partial void OnSelectedApiKeyFieldChanged(ProjectSecretApiKeyFieldOption? value)
    {
        if (value is null) return;
        Key = value.FieldName;
        IsSecret = value.IsSensitive;
    }

    public void SetApiKeys(IEnumerable<ApiKeyEntry> apiKeys)
    {
        var selectedItemId = SelectedApiKeyField?.ItemId;
        var selectedFieldId = SelectedApiKeyField?.FieldId;
        ApiKeyFields.Clear();
        foreach (var apiKey in (apiKeys ?? Array.Empty<ApiKeyEntry>()).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        foreach (var field in (apiKey.Fields ?? Array.Empty<ApiKeyFieldEntry>()).OrderBy(field => field.SortOrder))
            ApiKeyFields.Add(new ProjectSecretApiKeyFieldOption(apiKey.Id, apiKey.Name, field.Id, field.Label, field.Value, field.IsSensitive));
        SelectedApiKeyField = ApiKeyFields.FirstOrDefault(option =>
            option.ItemId == selectedItemId && option.FieldId == selectedFieldId);
    }

    public void OpenAdd(ProjectSecretVariableSourceKind source = ProjectSecretVariableSourceKind.Manual)
    {
        _original = null;
        Clear();
        SourceKind = source;
        IsOpen = true;
        NotifyMode();
    }

    public void OpenEdit(ProjectSecretVariableEntry variable)
    {
        _original = variable;
        Key = variable.Key;
        Value = variable.Value;
        Notes = variable.Notes;
        IsSecret = variable.IsSecret;
        SourceKind = variable.SourceKind;
        SelectedApiKeyField = ApiKeyFields.FirstOrDefault(option => option.ItemId == variable.ReferencedItemId && option.FieldId == variable.ReferencedFieldId);
        IsValueVisible = false;
        Error = "";
        IsOpen = true;
        NotifyMode();
    }

    [RelayCommand] private void Close() { IsOpen = false; Clear(); }
    [RelayCommand] private void UseManual() { SourceKind = ProjectSecretVariableSourceKind.Manual; SelectedApiKeyField = null; }
    [RelayCommand] private void UseReference() => SourceKind = ProjectSecretVariableSourceKind.ReferencedApiKey;
    [RelayCommand] private void UseImportedCopy() => SourceKind = ProjectSecretVariableSourceKind.ImportedApiKey;
    [RelayCommand] private void ToggleValue() => IsValueVisible = !IsValueVisible;

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";
        var key = Key.Trim();
        if (key.Length == 0) { Error = "Variable key is required."; return; }
        if (IsApiKeySource && SelectedApiKeyField is null) { Error = "Select an API Key field."; return; }
        if (SourceKind == ProjectSecretVariableSourceKind.ImportedApiKey &&
            !await root.ConfirmAsync("Import API Key copy", "Import an independent encrypted copy? Later API Key changes will not update this variable.", "Import copy"))
            return;

        var reference = SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? SelectedApiKeyField : null;
        var storedValue = SourceKind switch
        {
            ProjectSecretVariableSourceKind.ReferencedApiKey => "",
            ProjectSecretVariableSourceKind.ImportedApiKey => SelectedApiKeyField?.Value ?? "",
            _ => Value
        };
        var variable = new ProjectSecretVariableEntry(
            _original?.Id ?? Guid.NewGuid().ToString("N"), key, storedValue, IsSecret, Notes.Trim(),
            _original?.SortOrder ?? 0, SourceKind, reference?.ItemId ?? "", reference?.FieldId ?? "",
            reference?.FieldName ?? "", DateTimeOffset.UtcNow.ToString("O"));
        if (SaveRequested is null || await SaveRequested(variable))
            Close();
    }

    private void Clear()
    {
        Key = Value = Notes = Error = "";
        IsSecret = true;
        IsValueVisible = false;
        SourceKind = ProjectSecretVariableSourceKind.Manual;
        SelectedApiKeyField = null;
    }

    private void NotifyMode()
    {
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(Title));
    }
}

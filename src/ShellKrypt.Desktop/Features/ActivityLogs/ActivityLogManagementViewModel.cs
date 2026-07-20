using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Activity;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed partial class ActivityLogManagementViewModel : ObservableObject
{
    private readonly ActivityLogsRuntime _runtime;
    private readonly ActivityLogService _service;
    private readonly ActivityLogListViewModel _list;
    private readonly ActivityReportService _reports;
    private readonly Func<Task> _reload;

    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string error = string.Empty;
    [ObservableProperty] private bool isBusy;

    public ActivityLogManagementViewModel(
        ActivityLogsRuntime runtime,
        ActivityLogService service,
        ActivityLogListViewModel list,
        ActivityReportService reports,
        Func<Task> reload)
    {
        _runtime = runtime;
        _service = service;
        _list = list;
        _reports = reports;
        _reload = reload;
        _list.FilteredItemsChanged += (_, _) => NotifyCommandState();
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanExportAll => !IsBusy && _list.HasStoredItems;
    public bool CanExportFiltered => !IsBusy && _list.HasNarrowingFilter && _list.HasVisibleItems;
    public bool CanClear => !IsBusy && _list.HasStoredItems;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => NotifyCommandState();

    [RelayCommand(CanExecute = nameof(CanExportAll))]
    private Task ExportAllAsync() => ExportAsync(
        "All",
        _list.AllItemsInSelectedSortOrder.ToArray(),
        _list.TotalEvents,
        new("all", "all", "all", _list.AppliedFilters.Sort, false),
        CurrentVaultDisplayName);

    [RelayCommand(CanExecute = nameof(CanExportFiltered))]
    private Task ExportFilteredAsync() => ExportAsync(
        "Filtered",
        _list.FilteredItems.ToArray(),
        _list.TotalEvents,
        _list.AppliedFilters,
        CurrentVaultDisplayName);

    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        ResetMessages();
        IsBusy = true;
        try
        {
            if (!await _runtime.ConfirmDangerousActionAsync(
                    T("Activity.Clear.Title"),
                    T("Activity.Clear.Subtitle"),
                    T("Activity.Clear.Detail"),
                    T("Activity.Clear.Confirm")))
                return;

            var result = _service.Clear(_runtime.VaultPath, _runtime.IsUnlocked ? _runtime.VaultKey : null);
            if (!result.Success)
            {
                Error = T("Activity.Error.ClearFailed");
                return;
            }

            await _reload();
            var recordResult = _runtime.LogActivity("activity", "Activity logs cleared", "The current vault activity feed was cleared.", "warning", affectedItem: CurrentVaultDisplayName);
            if (!recordResult.Success)
                Error = T("Activity.Error.RecordFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetRecorderFailure() => Error = T("Activity.Error.RecordFailed");

    public void RefreshLocalization()
    {
        if (HasError)
            Error = T("Activity.Error.OperationFailed");
    }

    private async Task ExportAsync(
        string scope,
        IReadOnlyList<ActivityItemVm> items,
        int sourceTotalEvents,
        ActivityAppliedFilters filters,
        string vaultDisplayName)
    {
        ResetMessages();
        if (items.Count == 0)
        {
            Error = T("Activity.Export.NoLogs");
            return;
        }

        IsBusy = true;
        try
        {
            var suggestedName = $"ShellKrypt-{SanitizeFileName(vaultDisplayName)}-activity-{_reports.Now:yyyyMMdd-HHmmss}.json";
            var exportPath = await _runtime.PickSaveFileAsync(T("Activity.Export.DialogTitle"), suggestedName, ".json", [".json"], T("Activity.Export.FileType"));
            if (string.IsNullOrWhiteSpace(exportPath))
                return;

            var json = _reports.BuildJson(scope, vaultDisplayName, items, sourceTotalEvents, filters);
            await _reports.WriteAsync(exportPath, json);
            Status = T("Activity.Export.PlaintextWarning");
            var result = _runtime.LogActivity("activity", "Activity report exported", $"Saved {items.Count} activity log entries to {Path.GetFileName(exportPath)}.", "info", affectedItem: Path.GetFileName(exportPath));
            if (!result.Success)
                Error = T("Activity.Error.RecordFailed");
        }
        catch
        {
            Error = T("Activity.Error.ExportFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string CurrentVaultDisplayName => string.IsNullOrWhiteSpace(_runtime.VaultPath) ? T("Activity.CurrentVault") : Path.GetFileNameWithoutExtension(_runtime.VaultPath);
    private string T(string key, params object[] args) => _runtime.Localization.Get(key, args);

    private void ResetMessages() { Status = string.Empty; Error = string.Empty; }
    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanExportAll));
        OnPropertyChanged(nameof(CanExportFiltered));
        OnPropertyChanged(nameof(CanClear));
        ExportAllCommand.NotifyCanExecuteChanged();
        ExportFilteredCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "vault" : sanitized;
    }
}

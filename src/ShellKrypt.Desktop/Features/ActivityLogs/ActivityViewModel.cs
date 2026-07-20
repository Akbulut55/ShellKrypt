using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Activity;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed partial class ActivityViewModel : ViewModelBase
{
    private readonly ActivityLogsRuntime _runtime;
    private readonly ActivityLogService _service;
    private bool _active;

    [ObservableProperty] private int skippedCorruptEntries;
    [ObservableProperty] private bool hasLoadFailure;

    public ActivityViewModel(ActivityLogsRuntime runtime, ActivityLogService service, TimeProvider? timeProvider = null)
    {
        _runtime = runtime;
        _service = service;
        var clock = timeProvider ?? TimeProvider.System;
        List = new ActivityLogListViewModel(runtime.Localization, clock);
        Details = new ActivityLogDetailsViewModel(runtime.Localization, clock);
        Management = new ActivityLogManagementViewModel(runtime, service, List, new ActivityReportService(clock), ReloadAsync);
        List.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ActivityLogListViewModel.SelectedItem))
                Details.SelectedItem = List.SelectedItem;
        };
    }

    public ActivityLogListViewModel List { get; }
    public ActivityLogDetailsViewModel Details { get; }
    public ActivityLogManagementViewModel Management { get; }
    public bool HasCorruptionWarning => SkippedCorruptEntries > 0;
    public string CorruptionWarning => T(_runtime, "Activity.Warning.CorruptRows", SkippedCorruptEntries);
    public string LoadFailureMessage => T(_runtime, "Activity.Error.LoadFailed");

    partial void OnSkippedCorruptEntriesChanged(int value)
    {
        OnPropertyChanged(nameof(HasCorruptionWarning));
        OnPropertyChanged(nameof(CorruptionWarning));
    }

    public void Activate()
    {
        if (_active)
            return;
        _active = true;
        _runtime.ActivityChanged += OnActivityChanged;
        Reload();
    }

    public void Deactivate()
    {
        if (!_active)
            return;
        _active = false;
        _runtime.ActivityChanged -= OnActivityChanged;
    }

    [RelayCommand]
    private void Refresh() => Reload();

    public override void RefreshLocalization()
    {
        List.RefreshLocalization();
        Details.RefreshLocalization();
        Management.RefreshLocalization();
        OnPropertyChanged(nameof(CorruptionWarning));
        OnPropertyChanged(nameof(LoadFailureMessage));
    }

    private Task ReloadAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        var result = _service.Load(_runtime.VaultPath, _runtime.IsUnlocked ? _runtime.VaultKey : null);
        HasLoadFailure = !result.Success;
        SkippedCorruptEntries = result.Success ? result.SkippedCorruptEntries : 0;
        if (result.Success)
            List.Load(result.Entries);
    }

    private void OnActivityChanged(object? sender, ActivityRecorderChangedEventArgs args)
    {
        if (args.Result.Success)
            Reload();
        else
            Management.SetRecorderFailure();
    }
}

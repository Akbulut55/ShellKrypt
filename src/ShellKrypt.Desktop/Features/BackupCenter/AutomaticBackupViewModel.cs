using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class AutomaticBackupViewModel : ViewModelBase
{
    private readonly BackupCenterContext _context;
    private readonly BackupOperationState _operation;

    [ObservableProperty] private bool enabled;
    [ObservableProperty] private string directory = "";
    [ObservableProperty] private string passphrase = "";
    [ObservableProperty] private int retentionCount = BackupScheduleSettings.DefaultRetentionCount;
    [ObservableProperty] private AutomaticBackupFrequencyOption? selectedFrequencyOption;
    [ObservableProperty] private string status = "";

    internal AutomaticBackupViewModel(BackupCenterContext context, BackupOperationState operation)
    {
        _context = context;
        _operation = operation;
        FrequencyOptions.Add(new(BackupScheduleFrequency.Daily, "BackupCenter.Automatic.Frequency.Daily"));
        FrequencyOptions.Add(new(BackupScheduleFrequency.EveryThreeDays, "BackupCenter.Automatic.Frequency.EveryThreeDays"));
        FrequencyOptions.Add(new(BackupScheduleFrequency.Weekly, "BackupCenter.Automatic.Frequency.Weekly"));
        RefreshLocalization();
        Load();
        _context.AutomaticBackupChanged += (_, _) => RefreshState();
    }

    public ObservableCollection<AutomaticBackupFrequencyOption> FrequencyOptions { get; } = [];
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasSessionPassphrase => _context.HasAutomaticBackupPassphrase;
    public bool IsRunning => _context.IsAutomaticBackupRunning;
    public string SessionText => HasSessionPassphrase
        ? T("BackupCenter.Automatic.Session.Ready")
        : T("BackupCenter.Automatic.Session.Missing");

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnPassphraseChanged(string value)
    {
        _context.SetAutomaticBackupPassphrase(value);
        RefreshState();
    }

    public override void RefreshLocalization()
    {
        foreach (var option in FrequencyOptions)
            option.RefreshLocalization(_context.Localization);
        OnPropertyChanged(nameof(SelectedFrequencyOption));
        OnPropertyChanged(nameof(SessionText));
    }

    [RelayCommand]
    private void Configure() => Status = T("BackupCenter.Health.Automatic.ConfigureHint");

    [RelayCommand]
    private async Task BrowseDirectoryAsync()
    {
        var path = await _context.PickFolderAsync(T("BackupCenter.Automatic.Picker.DirectoryTitle"));
        if (!string.IsNullOrWhiteSpace(path))
            Directory = path;
    }

    [RelayCommand]
    private void Save()
    {
        _context.Schedule.Enabled = Enabled;
        _context.Schedule.BackupDirectory = Directory;
        _context.Schedule.Frequency = SelectedFrequencyOption?.Frequency ?? BackupScheduleFrequency.Daily;
        _context.Schedule.RetentionCount = RetentionCount;
        _context.SaveSchedule();
        Load();
        Status = T("BackupCenter.Automatic.Status.Saved");
    }

    [RelayCommand]
    private async Task RunNowAsync()
    {
        Save();
        await _operation.RunAsync(async () =>
        {
            var result = await _context.RunAutomaticBackupNowAsync();
            Status = result.Success
                ? T("BackupCenter.Automatic.Status.RunSuccess", Path.GetFileName(result.BackupPath ?? ""))
                : result.Message;
            _operation.Status = Status;
            RefreshState();
        });
    }

    private void Load()
    {
        _context.Schedule.Normalize();
        Enabled = _context.Schedule.Enabled;
        Directory = _context.Schedule.BackupDirectory;
        RetentionCount = _context.Schedule.RetentionCount;
        SelectedFrequencyOption = FrequencyOptions.FirstOrDefault(option => option.Frequency == _context.Schedule.Frequency)
            ?? FrequencyOptions.FirstOrDefault();
        RefreshState();
    }

    private void RefreshState()
    {
        if (!_context.HasAutomaticBackupPassphrase && !string.IsNullOrWhiteSpace(Passphrase))
            Passphrase = "";
        NotifyLocalized(nameof(HasSessionPassphrase), nameof(IsRunning), nameof(SessionText));
    }

    private string T(string key, params object[] args) => _context.T(key, args);
}

public sealed partial class AutomaticBackupFrequencyOption(
    BackupScheduleFrequency frequency,
    string labelKey) : ObservableObject
{
    public BackupScheduleFrequency Frequency { get; } = frequency;
    public string LabelKey { get; } = labelKey;
    [ObservableProperty] private string label = labelKey;
    public void RefreshLocalization(LocalizationService localization) => Label = localization.Get(LabelKey);
    public override string ToString() => Label;
}

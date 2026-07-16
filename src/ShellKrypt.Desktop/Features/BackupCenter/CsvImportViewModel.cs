using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Localization;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class CsvImportViewModel : ViewModelBase
{
    private readonly BackupCenterContext _context;
    private readonly BackupOperationState _operation;
    private readonly BackupHistoryViewModel _history;

    [ObservableProperty] private string importPath = "";
    [ObservableProperty] private string previewSummary = "";
    [ObservableProperty] private CsvDuplicateStrategyOption? selectedDuplicateStrategyOption;

    internal CsvImportViewModel(
        BackupCenterContext context,
        BackupOperationState operation,
        BackupHistoryViewModel history)
    {
        _context = context;
        _operation = operation;
        _history = history;
        ImportPath = context.History.LastCsvImportPath;
        DuplicateStrategyOptions.Add(new(VaultCsvDuplicateStrategy.SkipDuplicates, "BackupCenter.Csv.Duplicate.Skip"));
        DuplicateStrategyOptions.Add(new(VaultCsvDuplicateStrategy.OverwriteDuplicates, "BackupCenter.Csv.Duplicate.Overwrite"));
        DuplicateStrategyOptions.Add(new(VaultCsvDuplicateStrategy.ImportAll, "BackupCenter.Csv.Duplicate.ImportAll"));
        RefreshLocalization();
        SelectedDuplicateStrategyOption = DuplicateStrategyOptions[0];
    }

    public ObservableCollection<CsvDuplicateStrategyOption> DuplicateStrategyOptions { get; } = [];
    public ObservableCollection<VaultCsvImportRowPreview> PreviewRows { get; } = [];
    public bool HasPreview => PreviewRows.Count > 0;
    public VaultCsvDuplicateStrategy SelectedDuplicateStrategy
    {
        get => SelectedDuplicateStrategyOption?.Strategy ?? VaultCsvDuplicateStrategy.SkipDuplicates;
        set => SelectedDuplicateStrategyOption = DuplicateStrategyOptions.FirstOrDefault(option => option.Strategy == value)
            ?? DuplicateStrategyOptions[0];
    }

    partial void OnSelectedDuplicateStrategyOptionChanged(CsvDuplicateStrategyOption? value)
        => OnPropertyChanged(nameof(SelectedDuplicateStrategy));

    public override void RefreshLocalization()
    {
        foreach (var option in DuplicateStrategyOptions)
            option.RefreshLocalization(_context.Localization);
        OnPropertyChanged(nameof(SelectedDuplicateStrategyOption));
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await _context.PickOpenFileAsync(
            T("BackupCenter.Picker.Csv.Title"),
            [".csv"],
            T("BackupCenter.Picker.Csv.FileType"));
        if (!string.IsNullOrWhiteSpace(path))
            ImportPath = path;
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey))
            return;
        if (!ValidatePath())
            return;
        await _operation.RunAsync(async () =>
        {
            await LoadPreviewAsync(vaultPath, vaultKey);
            _operation.Status = T("BackupCenter.Status.CsvPreviewReady");
        });
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey))
            return;
        if (!ValidatePath())
            return;
        await _operation.RunAsync(async () =>
        {
            await _context.ClearClipboardAsync();
            if (!HasPreview)
                await LoadPreviewAsync(vaultPath, vaultKey);
            await _context.CsvImports.ImportAsync(vaultPath, vaultKey, ImportPath, SelectedDuplicateStrategy);
            _history.Record(BackupHistoryViewModel.CsvImport, "success", ImportPath, PreviewRows.Count, 0);
            _context.ClearAutomaticBackupPassphrase();
            _context.ReloadShell();
            _operation.Status = T("BackupCenter.Status.CsvImportFinished", SelectedDuplicateStrategyOption?.Label ?? SelectedDuplicateStrategy.ToString());
            _context.LogActivity(
                "transfer",
                "CSV import completed",
                $"Imported items from {Path.GetFileName(ImportPath)} using {SelectedDuplicateStrategy}.",
                "success",
                vaultPath,
                Path.GetFileName(ImportPath));
        });
    }

    private bool ValidatePath()
    {
        if (!string.IsNullOrWhiteSpace(ImportPath))
            return true;
        _operation.Status = T("BackupCenter.Status.EnterCsvPath");
        return false;
    }

    private async Task LoadPreviewAsync(string vaultPath, byte[] vaultKey)
    {
        var preview = await _context.CsvImports.PreviewAsync(vaultPath, vaultKey, ImportPath);
        PreviewRows.Clear();
        foreach (var row in preview.Rows)
            PreviewRows.Add(row);
        PreviewSummary = T("BackupCenter.Format.CsvSummary", preview.TotalRows, preview.NewRows, preview.DuplicateRows, preview.InvalidRows);
        OnPropertyChanged(nameof(HasPreview));
    }

    private string T(string key, params object[] args) => _context.T(key, args);
}

public sealed partial class CsvDuplicateStrategyOption(
    VaultCsvDuplicateStrategy strategy,
    string labelKey) : ObservableObject
{
    public VaultCsvDuplicateStrategy Strategy { get; } = strategy;
    public string LabelKey { get; } = labelKey;
    [ObservableProperty] private string label = labelKey;
    public void RefreshLocalization(LocalizationService localization) => Label = localization.Get(LabelKey);
    public override string ToString() => Label;
}

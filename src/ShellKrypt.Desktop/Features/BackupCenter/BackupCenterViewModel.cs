using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed class BackupCenterViewModel : ViewModelBase
{
    public BackupCenterViewModel(MainWindowViewModel root)
    {
        var context = new BackupCenterContext(root);
        Operation = new BackupOperationState();
        History = new BackupHistoryViewModel(context);
        Health = new BackupHealthViewModel(context, History);
        Encrypted = new EncryptedBackupViewModel(context, Operation, History);
        Plaintext = new PlaintextExportViewModel(context, Operation, History);
        Csv = new CsvImportViewModel(context, Operation, History);
        Automatic = new AutomaticBackupViewModel(context, Operation);
    }

    public BackupOperationState Operation { get; }
    public BackupHealthViewModel Health { get; }
    public EncryptedBackupViewModel Encrypted { get; }
    public PlaintextExportViewModel Plaintext { get; }
    public CsvImportViewModel Csv { get; }
    public AutomaticBackupViewModel Automatic { get; }
    public BackupHistoryViewModel History { get; }

    public override void RefreshLocalization()
    {
        Health.RefreshLocalization();
        Encrypted.RefreshLocalization();
        Plaintext.RefreshLocalization();
        Csv.RefreshLocalization();
        Automatic.RefreshLocalization();
        History.RefreshLocalization();
    }
}

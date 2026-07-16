using ShellKrypt.Desktop.Shell;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed class BackupCenterViewModel : ViewModelBase
{
    public BackupCenterViewModel(
        BackupCenterRuntime desktop,
        IAutomaticBackupController automaticBackups,
        IEncryptedVaultBackupService backups,
        IVaultPlaintextExportService plaintextExports,
        IVaultCsvImportService csvImports,
        IDesktopNavigation navigation)
    {
        var context = new BackupCenterContext(desktop, automaticBackups, backups, plaintextExports, csvImports, navigation);
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

using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public partial class SettingsViewModel
{
    private string FormatExportSummary(VaultSnapshotSummary summary)
        => T(
            "Settings.Format.ExportSummary",
            summary.ItemCount,
            summary.WebCount,
            summary.CardCount,
            summary.NoteCount,
            summary.AuthenticatorCount,
            summary.ApiKeyCount,
            summary.LabelCount,
            summary.FavoriteCount);

    private string FormatImportSummary(VaultSnapshotSummary summary)
        => T(
            "Settings.Format.ImportSummary",
            summary.ItemCount,
            summary.AuthenticatorCount,
            summary.ApiKeyCount,
            summary.LabelCount,
            summary.FavoriteCount);
}

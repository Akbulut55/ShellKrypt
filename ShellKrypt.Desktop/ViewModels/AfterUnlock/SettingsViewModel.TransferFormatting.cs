using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public partial class SettingsViewModel
{
    private static string FormatExportSummary(VaultSnapshotSummary summary)
        => $"Items: {summary.ItemCount} | Web: {summary.WebCount} | Cards: {summary.CardCount} | Notes: {summary.NoteCount} | Authenticator: {summary.AuthenticatorCount} | API Keys: {summary.ApiKeyCount} | Labels: {summary.LabelCount} | Favorites: {summary.FavoriteCount}";

    private static string FormatImportSummary(VaultSnapshotSummary summary)
        => $"Previewing import: {summary.ItemCount} items, {summary.AuthenticatorCount} authenticator accounts, {summary.ApiKeyCount} API keys, {summary.LabelCount} labels, {summary.FavoriteCount} favorites.";
}

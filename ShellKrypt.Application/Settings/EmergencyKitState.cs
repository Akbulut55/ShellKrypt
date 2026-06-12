namespace ShellKrypt.Application.Settings;

public sealed class EmergencyKitState
{
    public bool NoPasswordRecoveryAcknowledged { get; set; }
    public bool MasterPasswordStoredExternally { get; set; }
    public bool BackupPassphraseStoredExternally { get; set; }
    public bool BackupLocationKnown { get; set; }
    public bool BackupVerified { get; set; }
    public string LastChecklistExportPath { get; set; } = "";
    public string LastUpdatedAtUtc { get; set; } = "";

    public void Normalize()
    {
        LastChecklistExportPath = NormalizeText(LastChecklistExportPath);
        LastUpdatedAtUtc = NormalizeText(LastUpdatedAtUtc);
    }

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}

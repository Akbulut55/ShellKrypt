namespace ShellKrypt.Application.Backups;

public enum BackupScheduleFrequency
{
    Daily = 1,
    EveryThreeDays = 3,
    Weekly = 7
}

public sealed class BackupScheduleSettings
{
    public const int DefaultRetentionCount = 5;
    public const int MinRetentionCount = 1;
    public const int MaxRetentionCount = 30;

    public bool Enabled { get; set; }
    public string BackupDirectory { get; set; } = "";
    public BackupScheduleFrequency Frequency { get; set; } = BackupScheduleFrequency.Daily;
    public int RetentionCount { get; set; } = DefaultRetentionCount;

    public void Normalize()
    {
        BackupDirectory = NormalizeText(BackupDirectory);
        if (!Enum.IsDefined(typeof(BackupScheduleFrequency), Frequency))
            Frequency = BackupScheduleFrequency.Daily;

        RetentionCount = Math.Clamp(RetentionCount, MinRetentionCount, MaxRetentionCount);
    }

    public TimeSpan Interval => Frequency switch
    {
        BackupScheduleFrequency.EveryThreeDays => TimeSpan.FromDays(3),
        BackupScheduleFrequency.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.FromDays(1)
    };

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}

public sealed class AutomaticBackupState
{
    public string LastAttemptedAtUtc { get; set; } = "";
    public string LastSuccessfulAtUtc { get; set; } = "";
    public string LastVerifiedAtUtc { get; set; } = "";
    public string LastBackupPath { get; set; } = "";
    public string LastBackupFileName { get; set; } = "";
    public string LastStatus { get; set; } = "";
    public string LastError { get; set; } = "";

    public void Normalize()
    {
        LastAttemptedAtUtc = NormalizeText(LastAttemptedAtUtc);
        LastSuccessfulAtUtc = NormalizeText(LastSuccessfulAtUtc);
        LastVerifiedAtUtc = NormalizeText(LastVerifiedAtUtc);
        LastBackupPath = NormalizeText(LastBackupPath);
        LastBackupFileName = NormalizeText(LastBackupFileName);
        LastStatus = NormalizeText(LastStatus).ToLowerInvariant();
        LastError = NormalizeText(LastError);
    }

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}

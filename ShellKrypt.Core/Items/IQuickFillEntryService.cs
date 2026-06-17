namespace ShellKrypt.Core.Items;

public enum QuickFillFieldKind
{
    Username = 1,
    Password = 2,
    Text = 3,
    Secret = 4,
    Otp = 5
}

public enum QuickFillFieldSourceKind
{
    Owned = 1,
    WebLogin = 2,
    ApiKeyField = 3,
    Authenticator = 4
}

public sealed record QuickFillTargetRule(
    string ProcessName,
    string WindowTitleContains);

public sealed record QuickFillField(
    string Id,
    string Label,
    QuickFillFieldKind Kind,
    bool IsSensitive,
    int SortOrder,
    QuickFillFieldSourceKind SourceKind,
    string Value,
    string LinkedItemId,
    string LinkedFieldId,
    string LinkedFieldName);

public sealed record QuickFillEntryPayload(
    string Name,
    string Category,
    bool Enabled,
    QuickFillTargetRule Target,
    IReadOnlyList<QuickFillField> Fields,
    bool PressEnterAfterFill,
    string Notes);

public sealed record QuickFillEntryInput(
    string Name,
    string Category,
    bool Enabled,
    QuickFillTargetRule Target,
    IReadOnlyList<QuickFillField> Fields,
    bool PressEnterAfterFill,
    string Notes);

public sealed record QuickFillEntry(
    string Id,
    string Name,
    string Category,
    bool Enabled,
    QuickFillTargetRule Target,
    IReadOnlyList<QuickFillField> Fields,
    bool PressEnterAfterFill,
    string Notes,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public sealed record QuickFillTargetContext(
    string ProcessName,
    string WindowTitle,
    nint WindowHandle = 0);

public interface IQuickFillEntryService
{
    Task<IReadOnlyList<QuickFillEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<QuickFillEntry> AddAsync(string vaultPath, byte[] vaultKey, QuickFillEntryInput input, CancellationToken ct = default);
    Task<QuickFillEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, QuickFillEntryInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

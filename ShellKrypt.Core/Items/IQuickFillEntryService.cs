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
    Authenticator = 4,
    CreditCard = 5
}

public enum QuickFillSequenceStepKind
{
    Field = 1,
    Keystroke = 2,
    LiteralText = 3,
    Delay = 4
}

public enum QuickFillKeystrokeKind
{
    Tab = 1,
    Enter = 2,
    Escape = 3,
    Space = 4,
    Backspace = 5,
    Delete = 6,
    ArrowLeft = 7,
    ArrowRight = 8,
    ArrowUp = 9,
    ArrowDown = 10,
    Home = 11,
    End = 12,
    PageUp = 13,
    PageDown = 14,
    Insert = 15,
    F1 = 16,
    F2 = 17,
    F3 = 18,
    F4 = 19,
    F5 = 20,
    F6 = 21,
    F7 = 22,
    F8 = 23,
    F9 = 24,
    F10 = 25,
    F11 = 26,
    F12 = 27,
    A = 28,
    B = 29,
    C = 30,
    D = 31,
    E = 32,
    F = 33,
    G = 34,
    H = 35,
    I = 36,
    J = 37,
    K = 38,
    L = 39,
    M = 40,
    N = 41,
    O = 42,
    P = 43,
    Q = 44,
    R = 45,
    S = 46,
    T = 47,
    U = 48,
    V = 49,
    W = 50,
    X = 51,
    Y = 52,
    Z = 53,
    D0 = 54,
    D1 = 55,
    D2 = 56,
    D3 = 57,
    D4 = 58,
    D5 = 59,
    D6 = 60,
    D7 = 61,
    D8 = 62,
    D9 = 63
}

[Flags]
public enum QuickFillKeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Meta = 8
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

public sealed record QuickFillSequenceStep(
    string Id,
    QuickFillSequenceStepKind Kind,
    int SortOrder,
    string FieldId,
    QuickFillKeystrokeKind Keystroke,
    string Text,
    int DelayMilliseconds,
    QuickFillKeyModifiers Modifiers = QuickFillKeyModifiers.None,
    int RepeatCount = 1);

public sealed record QuickFillEntryPayload(
    string Name,
    string Category,
    bool Enabled,
    QuickFillTargetRule Target,
    IReadOnlyList<QuickFillField> Fields,
    bool PressEnterAfterFill,
    string Notes,
    IReadOnlyList<QuickFillSequenceStep>? SequenceSteps = null);

public sealed record QuickFillEntryInput(
    string Name,
    string Category,
    bool Enabled,
    QuickFillTargetRule Target,
    IReadOnlyList<QuickFillField> Fields,
    bool PressEnterAfterFill,
    string Notes,
    IReadOnlyList<QuickFillSequenceStep>? SequenceSteps = null);

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
    string UpdatedAtUtc,
    IReadOnlyList<QuickFillSequenceStep>? SequenceSteps = null);

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

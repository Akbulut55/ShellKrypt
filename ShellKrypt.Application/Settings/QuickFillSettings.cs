namespace ShellKrypt.Application.Settings;

public sealed class QuickFillSettings
{
    public const string DefaultShortcut = "Ctrl+Alt+K";

    public bool GlobalHotkeyEnabled { get; set; } = true;
    public string GlobalShortcut { get; set; } = DefaultShortcut;
    public string AutoTypeAcknowledgedAtUtc { get; set; } = "";

    public bool HasAutoTypeAcknowledgement => !string.IsNullOrWhiteSpace(AutoTypeAcknowledgedAtUtc);

    public void Normalize()
    {
        GlobalShortcut = string.IsNullOrWhiteSpace(GlobalShortcut)
            ? DefaultShortcut
            : GlobalShortcut.Trim();
        AutoTypeAcknowledgedAtUtc = string.IsNullOrWhiteSpace(AutoTypeAcknowledgedAtUtc)
            ? ""
            : AutoTypeAcknowledgedAtUtc.Trim();
    }
}

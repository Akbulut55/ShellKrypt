namespace ShellKrypt.Application.Ports;

public interface IClipboardPort
{
    Task CopyAsync(string text, TimeSpan clearAfter, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

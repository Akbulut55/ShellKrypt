namespace ShellKrypt.Core.Items;

public sealed record WebPayload(
    string Title,
    string Url,
    string Username,
    string Password,
    string Notes
)
{
    public string Email { get; init; } = "";
}

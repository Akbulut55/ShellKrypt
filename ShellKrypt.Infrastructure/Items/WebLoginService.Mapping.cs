using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class WebLoginService
{
    private static WebPayload ToPayload(WebLoginInput input)
        => new(
            Title: input.Title.Trim(),
            Url: input.Url.Trim(),
            Username: input.Username.Trim(),
            Password: input.Password,
            Notes: input.Notes.Trim())
        {
            Email = input.Email.Trim()
        };

    private static WebLoginEntry ToEntry(VaultItemHeader header, WebPayload payload)
        => new(
            Id: header.Id,
            Title: payload.Title,
            Url: payload.Url,
            Username: payload.Username,
            Email: payload.Email,
            Password: payload.Password,
            Notes: payload.Notes,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);
}

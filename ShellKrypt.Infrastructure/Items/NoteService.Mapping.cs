using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class NoteService
{
    private static NotePayload ToPayload(NoteInput input)
        => new(
            Title: input.Title.Trim(),
            Content: input.Content);

    private static NoteEntry ToEntry(VaultItemHeader header, NotePayload payload)
        => new(
            Id: header.Id,
            Title: payload.Title,
            Content: payload.Content,
            Favorite: header.Favorite,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);
}

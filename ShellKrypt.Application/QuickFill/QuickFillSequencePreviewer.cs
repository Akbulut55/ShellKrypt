using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.QuickFill;

public static class QuickFillSequencePreviewer
{
    public static IReadOnlyList<string> BuildPreview(QuickFillEntry entry)
        => ChooseFillFields(entry)
            .Select(DescribeField)
            .ToArray();

    public static IReadOnlyList<QuickFillField> ChooseFillFields(QuickFillEntry entry)
    {
        var fields = entry.Fields.OrderBy(field => field.SortOrder).ToArray();
        var username = fields.FirstOrDefault(field => field.Kind == QuickFillFieldKind.Username);
        var secret = fields.FirstOrDefault(field => field.Kind is QuickFillFieldKind.Password or QuickFillFieldKind.Secret or QuickFillFieldKind.Otp);
        if (username is not null && secret is not null)
            return [username, secret];

        return fields;
    }

    private static string DescribeField(QuickFillField field)
        => field.IsSensitive
            ? $"{field.Label} (masked)"
            : field.Label;
}

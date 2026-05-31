using System;
using System.Collections.Generic;
using System.Globalization;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed class PageChipVm
{
    public PageChipVm(int number, bool isCurrent)
    {
        Number = number;
        IsCurrent = isCurrent;
    }

    public int Number { get; }
    public bool IsCurrent { get; }
    public string Label => Number.ToString(CultureInfo.InvariantCulture);
}

public sealed class AllItemEntry
{
    public AllItemEntry(
        string id,
        ItemType type,
        string title,
        string nameSubtitle,
        string identifierText,
        IReadOnlyList<string> labels,
        string searchText,
        bool favorite,
        string createdAtUtc,
        string updatedAtUtc,
        string copyValue,
        int expiryMonth = 0,
        int expiryYear = 0)
    {
        Id = id;
        Type = type;
        Title = title;
        NameSubtitle = nameSubtitle;
        IdentifierText = identifierText;
        Labels = labels;
        SearchText = searchText;
        Favorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        CopyValue = copyValue;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
    }

    public string Id { get; }
    public ItemType Type { get; }
    public string Title { get; }
    public string NameSubtitle { get; }
    public string IdentifierText { get; }
    public IReadOnlyList<string> Labels { get; }
    public string SearchText { get; }
    public bool Favorite { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; }
    public string CopyValue { get; }
    public int ExpiryMonth { get; }
    public int ExpiryYear { get; }

    public string TypeLabel => Type.ToString();

    public string DisplayTypeLabel => Type switch
    {
        ItemType.Web => "LOGIN",
        ItemType.Card => "CARD",
        ItemType.Note => "MARKDOWN NOTE",
        ItemType.Authenticator => "AUTHENTICATOR",
        ItemType.ApiKey => "API KEY",
        _ => TypeLabel.ToUpperInvariant()
    };

    public string IconGlyph => Type switch
    {
        ItemType.Web => "WB",
        ItemType.Card => "CC",
        ItemType.Note => "MD",
        ItemType.Authenticator => "AU",
        ItemType.ApiKey => "AK",
        _ => "IT"
    };

    public string IconBackground => Type switch
    {
        ItemType.Web => "TypeWebBackgroundBrush",
        ItemType.Card => "TypeCardBackgroundBrush",
        ItemType.Note => "TypeNoteBackgroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorBackgroundBrush",
        ItemType.ApiKey => "TypeApiKeyBackgroundBrush",
        _ => "InfoMutedBrush"
    };

    public string IconForeground => Type switch
    {
        ItemType.Web => "TypeWebForegroundBrush",
        ItemType.Card => "TypeCardForegroundBrush",
        ItemType.Note => "TypeNoteForegroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorForegroundBrush",
        ItemType.ApiKey => "TypeApiKeyForegroundBrush",
        _ => "TextPrimaryBrush"
    };

    public string TypeBadgeBackground => Type switch
    {
        ItemType.Web => "TypeWebBackgroundBrush",
        ItemType.Card => "TypeCardBackgroundBrush",
        ItemType.Note => "TypeNoteBackgroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorBackgroundBrush",
        ItemType.ApiKey => "TypeApiKeyBackgroundBrush",
        _ => "InfoMutedBrush"
    };

    public string TypeBadgeForeground => Type switch
    {
        ItemType.Web => "TypeWebForegroundBrush",
        ItemType.Card => "TypeCardForegroundBrush",
        ItemType.Note => "TypeNoteForegroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorForegroundBrush",
        ItemType.ApiKey => "TypeApiKeyForegroundBrush",
        _ => "TextPrimaryBrush"
    };

    public string FavoriteGlyph => Favorite ? "â˜…" : string.Empty;
    public string LabelsDisplay => Labels.Count == 0 ? "No labels" : string.Join(", ", Labels);
    public string IdentifierDisplay => string.IsNullOrWhiteSpace(IdentifierText) ? "N/A" : IdentifierText.Trim();
    public string NameSubtitleDisplay => string.IsNullOrWhiteSpace(NameSubtitle) ? "Encrypted vault item" : NameSubtitle.Trim();
    public string UpdatedDisplay => FormatRelativeDate(UpdatedAtUtc);
    public string UpdatedAbsoluteDisplay => FormatAbsoluteDate(UpdatedAtUtc);
    public string CreatedDisplay => FormatAbsoluteDate(CreatedAtUtc);
    public bool IsCardExpiryUrgent => TryGetExpiryDate(out var expiry) &&
                                      expiry >= DateTime.Today &&
                                      expiry <= DateTime.Today.AddMonths(3);

    public bool IsRecent(int recentWindowDays = 30)
    {
        if (!TryParseDate(UpdatedAtUtc, out var updated))
            return false;

        return updated >= DateTimeOffset.UtcNow.AddDays(-recentWindowDays);
    }

    private static string FormatRelativeDate(string? value)
    {
        if (!TryParseDate(value, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var now = DateTimeOffset.Now;
        var delta = now - local;

        if (delta < TimeSpan.Zero)
            return local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)} minute{Pluralize(delta.TotalMinutes)} ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)} hour{Pluralize(delta.TotalHours)} ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)} day{Pluralize(delta.TotalDays)} ago";
        if (local.Year == now.Year)
            return local.ToString("MMM d", CultureInfo.InvariantCulture);

        return local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatAbsoluteDate(string? value)
    {
        if (!TryParseDate(value, out var parsed))
            return "Unknown";

        return parsed.ToLocalTime().ToString("MMM d, yyyy '|' HH:mm", CultureInfo.InvariantCulture);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset parsed)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed);

    private bool TryGetExpiryDate(out DateTime expiry)
    {
        expiry = DateTime.MaxValue;

        if (Type != ItemType.Card || ExpiryMonth is < 1 or > 12 || ExpiryYear < 2000)
            return false;

        expiry = new DateTime(ExpiryYear, ExpiryMonth, DateTime.DaysInMonth(ExpiryYear, ExpiryMonth));
        return true;
    }

    private static string Pluralize(double value)
        => Math.Abs(value) >= 2 ? "s" : string.Empty;
}

internal enum AllItemsSortMode
{
    UpdatedDescending,
    Alphabetical,
    TypeThenTitle
}

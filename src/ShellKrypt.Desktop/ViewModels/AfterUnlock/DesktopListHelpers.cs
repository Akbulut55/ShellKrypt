using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public static class DesktopPagination
{
    public static int GetTotalPages(int itemCount, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pageSize, 0);
        return Math.Max(1, (int)Math.Ceiling(Math.Max(0, itemCount) / (double)pageSize));
    }

    public static int ClampPage(int currentPage, int itemCount, int pageSize)
        => Math.Clamp(currentPage, 1, GetTotalPages(itemCount, pageSize));

    public static IEnumerable<T> Page<T>(IEnumerable<T> items, int currentPage, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pageSize, 0);

        var page = Math.Max(1, currentPage);
        return items.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

public static class DesktopFilterOptions
{
    public static void RebuildStringOptions(
        ObservableCollection<string> options,
        string defaultOption,
        IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);

        options.Clear();
        options.Add(defaultOption);

        foreach (var value in values
                     .Select(value => value?.Trim())
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(value);
        }
    }

    public static string KeepSelectedOrDefault(
        IEnumerable<string> options,
        string? selected,
        string defaultOption)
    {
        var selectedValue = selected ?? defaultOption;
        return options.Any(option => string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase))
            ? selectedValue
            : defaultOption;
    }

    public static string KeepSelectedOrEmpty(IEnumerable<string> options, string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
            return "";

        return options.Any(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase))
            ? selected
            : "";
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShellKrypt.Desktop.Services;

public sealed class ActivityLogStore
{
    private const int MaxEntries = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<ActivityLogEntry> Load()
    {
        try
        {
            if (!File.Exists(DefaultPaths.ActivityLogPath))
                return [];

            return JsonSerializer.Deserialize<List<ActivityLogEntry>>(File.ReadAllText(DefaultPaths.ActivityLogPath), JsonOptions)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Append(ActivityLogEntry entry)
    {
        var entries = Load()
            .OrderByDescending(x => x.TimestampUtc, StringComparer.Ordinal)
            .Take(MaxEntries - 1)
            .ToList();

        entries.Insert(0, entry);
        Save(entries);
    }

    public void Clear() => Save([]);

    private static void Save(IReadOnlyList<ActivityLogEntry> entries)
    {
        var dir = Path.GetDirectoryName(DefaultPaths.ActivityLogPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(DefaultPaths.ActivityLogPath, JsonSerializer.Serialize(entries, JsonOptions));
    }
}

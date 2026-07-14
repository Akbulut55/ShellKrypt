using System.Text.Json;
using ShellKrypt.Application.Ports;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Infrastructure.Services;

public sealed class FileAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(DefaultPaths.SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(DefaultPaths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(DefaultPaths.SettingsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(DefaultPaths.SettingsPath, json);
    }
}

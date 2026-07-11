using ShellKrypt.Application.Settings;

namespace ShellKrypt.Application.Ports;

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

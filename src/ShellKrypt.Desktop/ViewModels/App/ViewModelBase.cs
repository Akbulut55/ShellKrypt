using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        public virtual void RefreshLocalization()
        {
        }

        protected static string T(MainWindowViewModel root, string key, params object[] args)
            => root.Localization.Get(key, args);

    protected static string T(LocalizationService localization, string key, params object[] args)
        => localization.Get(key, args);

    protected static string T(DesktopFeatureServices desktop, string key, params object[] args)
        => desktop.Localization.Get(key, args);

        protected void NotifyLocalized(params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
                OnPropertyChanged(propertyName);
        }
    }
}

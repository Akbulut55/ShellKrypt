using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        public virtual void RefreshLocalization()
        {
        }

        protected static string T(MainWindowViewModel root, string key, params object[] args)
            => root.Localization.Get(key, args);

        protected void NotifyLocalized(params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
                OnPropertyChanged(propertyName);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NavItemVm : ObservableObject
{
    public string Key { get; }
    public string Title { get; }

    public NavItemVm(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public override string ToString() => Title;
}
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NavGroupVm : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _titleKey;

    public NavGroupVm(string key, IEnumerable<NavItemVm> items, LocalizationService localization)
    {
        Key = key;
        _localization = localization;
        _titleKey = $"Sidebar.Group.{key}";
        foreach (var item in items)
            Items.Add(item);
    }

    public string Key { get; }
    public ObservableCollection<NavItemVm> Items { get; } = new();
    public string Title => _localization.Get(_titleKey);

    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));
}

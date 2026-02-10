using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase current;

    public MainWindowViewModel()
    {
        Current = new WelcomeViewModel(this);
    }

    public void NavigateTo(ViewModelBase vm) => Current = vm;
}
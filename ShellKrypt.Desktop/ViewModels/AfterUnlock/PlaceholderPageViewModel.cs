namespace ShellKrypt.Desktop.ViewModels;

public sealed class PlaceholderPageViewModel 
    : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }

    public PlaceholderPageViewModel(string title, string message)
    {
        Title = title; 
        Message = message; 
    }
}

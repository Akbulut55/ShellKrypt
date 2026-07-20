using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public partial class ActivityView : UserControl
{
    public ActivityView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
            DataContext = ActivityViewDesignData.CreatePopulated();
    }
}

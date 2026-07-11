using ShellKrypt.Desktop.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class DesktopListHelperTests
{
    [Fact]
    public void DesktopPagination_ClampsAndSlicesPages()
    {
        Assert.Equal(1, DesktopPagination.GetTotalPages(0, 5));
        Assert.Equal(3, DesktopPagination.GetTotalPages(11, 5));
        Assert.Equal(1, DesktopPagination.ClampPage(-5, 11, 5));
        Assert.Equal(3, DesktopPagination.ClampPage(99, 11, 5));

        var page = DesktopPagination.Page(Enumerable.Range(1, 11), 3, 5).ToArray();

        Assert.Equal([11], page);
    }

    [Fact]
    public void DesktopFilterOptions_RebuildsSortedDistinctOptionsAndKeepsSelection()
    {
        var options = new ObservableCollection<string>();

        DesktopFilterOptions.RebuildStringOptions(options, "All", ["beta", " Alpha ", "", "alpha", null, "Gamma"]);

        Assert.Equal(["All", "Alpha", "beta", "Gamma"], options);
        Assert.Equal("alpha", DesktopFilterOptions.KeepSelectedOrDefault(options, "alpha", "All"));
        Assert.Equal("All", DesktopFilterOptions.KeepSelectedOrDefault(options, "missing", "All"));
        Assert.Equal("Gamma", DesktopFilterOptions.KeepSelectedOrEmpty(options, "Gamma"));
        Assert.Equal("", DesktopFilterOptions.KeepSelectedOrEmpty(options, "missing"));
    }
}

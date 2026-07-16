using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class ItemSearchMatcherTests
{
    [Fact]
    public void EmptyQueryMatches()
    {
        Assert.True(ItemSearchMatcher.Matches("", "github.com"));
        Assert.True(ItemSearchMatcher.Matches("   ", "github.com"));
    }

    [Fact]
    public void QueryTokensCanMatchAcrossFields()
    {
        Assert.True(ItemSearchMatcher.Matches(
            "github octo",
            "github.com",
            "octo@example.com"));
    }

    [Fact]
    public void MissingTokenDoesNotMatch()
    {
        Assert.False(ItemSearchMatcher.Matches(
            "github stripe",
            "github.com",
            "octo@example.com"));
    }
}

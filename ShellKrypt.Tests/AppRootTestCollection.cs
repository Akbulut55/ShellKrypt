using Xunit;

namespace ShellKrypt.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppRootTestCollection
{
    public const string Name = "App root environment";
}

using System.Text.Json;
using ShellKrypt.Application.Items;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed partial class ProjectSecretService : IProjectSecretService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public ProjectSecretService(IItemRepository repo)
    {
        _repo = repo;
    }
}

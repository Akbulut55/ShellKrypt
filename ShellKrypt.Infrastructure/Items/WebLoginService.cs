using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class WebLoginService : IWebLoginService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public WebLoginService(IItemRepository repo)
    {
        _repo = repo;
    }
}

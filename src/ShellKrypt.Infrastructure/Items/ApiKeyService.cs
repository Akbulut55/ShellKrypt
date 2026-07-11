using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ApiKeyService : IApiKeyService
{
    private const string DefaultFieldType = "API Key";
    private const string DefaultEnvironment = "Production";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public ApiKeyService(IItemRepository repo)
    {
        _repo = repo;
    }
}

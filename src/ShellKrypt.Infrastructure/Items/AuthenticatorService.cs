using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService : IAuthenticatorService
{
    private const string DefaultAlgorithm = "HMAC-SHA1";
    private const int DefaultDigits = 6;
    private const int DefaultPeriod = 30;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public AuthenticatorService(IItemRepository repo)
    {
        _repo = repo;
    }
}

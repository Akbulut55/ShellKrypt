using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class CardService : ICardService
{
    private const int CardNumberMaxDigits = 16;
    private const int CvcMaxDigits = 4;
    private const string DefaultCardType = "Credit Card";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public CardService(IItemRepository repo)
    {
        _repo = repo;
    }
}

using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class ApiKeyService : IApiKeyService
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

    public async Task<IReadOnlyList<ApiKeyEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var apiKeys = new List<ApiKeyEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.ApiKey))
        {
            var payload = DecryptPayload(vaultKey, row.EncryptedPayload);
            if (payload is null)
                continue;

            apiKeys.Add(ToEntry(row.Header, payload));
        }

        return apiKeys;
    }

    public async Task<ApiKeyEntry> AddAsync(string vaultPath, byte[] vaultKey, ApiKeyInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.ApiKey,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<ApiKeyEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        ApiKeyInput input,
        CancellationToken ct = default)
    {
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.ApiKey,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);

    private static byte[] EncryptPayload(byte[] vaultKey, ApiKeyPayload payload)
        => AesGcmBlob.Encrypt(vaultKey, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static ApiKeyPayload? DecryptPayload(byte[] vaultKey, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<ApiKeyPayload>(AesGcmBlob.Decrypt(vaultKey, encryptedPayload), JsonOpts);

    private static ApiKeyPayload ToPayload(ApiKeyInput input)
    {
        var fields = NormalizeFields(input.Fields).ToArray();
        if (fields.Length == 0)
            throw new InvalidOperationException("Add at least one API key field.");

        return new ApiKeyPayload(
            Name: NormalizeRequired(input.Name, "Name is required."),
            Provider: input.Provider.Trim(),
            Environment: string.IsNullOrWhiteSpace(input.Environment) ? DefaultEnvironment : input.Environment.Trim(),
            Notes: input.Notes.Trim(),
            Fields: fields);
    }

    private static IEnumerable<ApiKeyFieldPayload> NormalizeFields(IEnumerable<ApiKeyFieldInput>? fields)
    {
        var order = 0;
        foreach (var field in fields ?? Array.Empty<ApiKeyFieldInput>())
        {
            if (string.IsNullOrWhiteSpace(field.Label) && string.IsNullOrWhiteSpace(field.Value))
                continue;

            if (string.IsNullOrWhiteSpace(field.Label))
                throw new InvalidOperationException("Every API key field needs a label.");

            if (string.IsNullOrWhiteSpace(field.Value))
                throw new InvalidOperationException($"Field \"{field.Label.Trim()}\" needs a value.");

            yield return new ApiKeyFieldPayload(
                Id: string.IsNullOrWhiteSpace(field.Id) ? Guid.NewGuid().ToString("N") : field.Id.Trim(),
                Label: field.Label.Trim(),
                FieldType: string.IsNullOrWhiteSpace(field.FieldType) ? DefaultFieldType : field.FieldType.Trim(),
                Value: field.Value,
                IsSensitive: field.IsSensitive,
                IsCopyable: field.IsCopyable,
                SortOrder: field.SortOrder <= 0 ? order : field.SortOrder);
            order++;
        }
    }

    private static ApiKeyEntry ToEntry(VaultItemHeader header, ApiKeyPayload payload)
        => new(
            Id: header.Id,
            Name: payload.Name,
            Provider: payload.Provider,
            Environment: string.IsNullOrWhiteSpace(payload.Environment) ? DefaultEnvironment : payload.Environment,
            Notes: payload.Notes,
            Fields: payload.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field => new ApiKeyFieldEntry(
                    field.Id,
                    field.Label,
                    field.FieldType,
                    field.Value,
                    field.IsSensitive,
                    field.IsCopyable,
                    field.SortOrder))
                .ToArray(),
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);

        return trimmed;
    }
}

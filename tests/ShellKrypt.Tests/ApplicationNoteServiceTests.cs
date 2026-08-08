using ShellKrypt.Application.Notes;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class ApplicationNoteServiceTests
{
    [Fact]
    public async Task Add_NormalizesInputAndUsesInjectedIdentityAndClock()
    {
        var store = new RecordingStore();
        var now = new DateTimeOffset(2026, 7, 21, 10, 30, 0, TimeSpan.Zero);
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var service = new NoteService(store, new FixedTimeProvider(now), () => id);

        var result = await service.AddAsync("vault", [1], new NoteInput("  Runbook  ", null, true));

        Assert.True(result.Success);
        Assert.Equal("11111111222233334444555555555555", result.Entry!.Id);
        Assert.Equal("Runbook", result.Entry.Title);
        Assert.Equal("", result.Entry.Content);
        Assert.Equal(now.ToString("O"), result.Entry.CreatedAtUtc);
        Assert.Equal(result.Entry.CreatedAtUtc, result.Entry.UpdatedAtUtc);
        Assert.Equal(result.Entry, store.Inserted);
    }

    [Fact]
    public async Task EmptyTitle_ReturnsValueFreeValidationFailureWithoutWriting()
    {
        var store = new RecordingStore();
        var service = new NoteService(store);

        var result = await service.AddAsync("vault", [1], new NoteInput("  ", "secret", false));

        Assert.False(result.Success);
        Assert.Null(result.Entry);
        Assert.Equal(NoteFailureKind.ValidationFailed, result.FailureKind);
        Assert.Null(store.Inserted);
    }

    [Fact]
    public async Task StoreException_IsMappedWithoutExceptionOrInputValues()
    {
        var service = new NoteService(new ThrowingStore());

        var result = await service.AddAsync("/private/vault", [1], new NoteInput("Private title", "Private body", false));

        Assert.False(result.Success);
        Assert.Null(result.Entry);
        Assert.Equal(NoteFailureKind.WriteFailed, result.FailureKind);
        Assert.DoesNotContain("Private", result.ToString(), StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingStore : INoteStore
    {
        public NoteEntry? Inserted { get; private set; }
        public Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
            => Task.FromResult(NoteLoadResult.Empty);
        public Task<NoteOperationResult> InsertAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
        {
            Inserted = entry;
            return Task.FromResult(NoteOperationResult.Succeeded);
        }
        public Task<NoteOperationResult> UpdateAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
            => Task.FromResult(NoteOperationResult.Succeeded);
        public Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
            => Task.FromResult(NoteOperationResult.Succeeded);
    }

    private sealed class ThrowingStore : INoteStore
    {
        public Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
            => throw new InvalidOperationException("/private/vault Private body");
        public Task<NoteOperationResult> InsertAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("/private/vault Private body");
        public Task<NoteOperationResult> UpdateAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("/private/vault Private body");
        public Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
            => throw new InvalidOperationException("/private/vault Private body");
    }
}

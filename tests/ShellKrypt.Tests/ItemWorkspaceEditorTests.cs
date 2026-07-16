using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;
using ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Desktop.Shell.Dialogs;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
public sealed class ItemWorkspaceEditorTests
{
    private readonly LocalizationService _localization = new();
    private readonly ItemWorkspaceRuntime _runtime;

    public ItemWorkspaceEditorTests()
    {
        _runtime = new ItemWorkspaceRuntime(
            new VaultSessionController(new AppState()),
            _localization,
            new StubActivityRecorder(),
            new StubSecureClipboard());
    }

    [Fact]
    public void WebLoginEditor_CancelEditRestoresSnapshot()
    {
        var editor = new WebLoginEditorViewModel(
            _runtime,
            new StubWebLoginService(),
            new CapturingPasswordGenerator(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
        var row = new WebLoginRowVm(
            _localization,
            "login-1",
            "Example",
            "user",
            "secret",
            "https://example.com",
            "original notes",
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z",
            false,
            "user@example.com");

        editor.OpenDetails(row);
        editor.BeginEditCommand.Execute(null);
        editor.Title = "Changed";
        editor.Password = "changed-secret";
        editor.CancelEditCommand.Execute(null);

        Assert.Equal(ItemEditorMode.Details, editor.Mode);
        Assert.Equal("Example", editor.Title);
        Assert.Equal("secret", editor.Password);
        Assert.False(editor.IsPasswordVisible);
    }

    [Fact]
    public void WebLoginEditor_GeneratePasswordUsesReusableGeneratorContract()
    {
        var generator = new CapturingPasswordGenerator();
        var editor = new WebLoginEditorViewModel(
            _runtime,
            new StubWebLoginService(),
            generator,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

        editor.OpenAdd();
        editor.GeneratePasswordCommand.Execute(null);

        Assert.Equal("generated-password", editor.Password);
        Assert.True(editor.IsPasswordVisible);
        Assert.Equal(new PasswordGenerationOptions(32, true, true, true, true), generator.LastOptions);
    }

    [Fact]
    public void WebLoginEditor_DetailsUsesRichSizeAndResetsRevealWhenEditing()
    {
        var editor = CreateWebLoginEditor();
        editor.OpenDetails(CreateWebLoginRow());

        Assert.Equal(ModalShellSize.ItemDetails, editor.ModalSize);
        Assert.DoesNotContain("secret", editor.PasswordDisplay, StringComparison.Ordinal);

        editor.TogglePasswordVisibilityCommand.Execute(null);
        Assert.True(editor.IsPasswordVisible);
        editor.BeginEditCommand.Execute(null);

        Assert.Equal(ModalShellSize.Standard, editor.ModalSize);
        Assert.False(editor.IsPasswordVisible);
    }

    [Fact]
    public void WebLoginEditor_CloseDetailsDoesNotMutateSource()
    {
        var row = CreateWebLoginRow();
        var editor = CreateWebLoginEditor();
        editor.OpenDetails(row);
        editor.Title = "Unsaved";

        editor.CloseCommand.Execute(null);

        Assert.False(editor.IsOpen);
        Assert.Equal("Example", row.Title);
    }

    [Fact]
    public void CardEditor_DeleteCancelReturnsToDetailsWithoutChangingValues()
    {
        var editor = new CardEditorViewModel(
            _runtime,
            new StubCardService(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
        var row = new CardRowVm(
            _localization,
            "card-1",
            "Personal",
            "Bank",
            "Owner",
            "4242424242424242",
            "09",
            "2030",
            "123",
            "notes",
            "Visa",
            "Credit Card",
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z");

        editor.OpenDetails(row);
        editor.BeginDeleteCommand.Execute(null);
        Assert.Equal(ItemEditorMode.ConfirmDelete, editor.Mode);

        editor.CancelDeleteCommand.Execute(null);

        Assert.Equal(ItemEditorMode.Details, editor.Mode);
        Assert.Equal("Personal", editor.Title);
        Assert.Equal("4242 4242 4242 4242", editor.Number);
    }

    [Fact]
    public void CardEditor_PreviewAlwaysMasksNumberAndNeverContainsCvc()
    {
        var editor = new CardEditorViewModel(
            _runtime,
            new StubCardService(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
        editor.OpenDetails(CreateCardRow());

        Assert.Equal(ModalShellSize.ItemDetails, editor.ModalSize);
        Assert.Contains("4242", editor.CardPreviewNumber, StringComparison.Ordinal);
        Assert.DoesNotContain("4242424242424242", editor.CardPreviewNumber, StringComparison.Ordinal);
        Assert.DoesNotContain("123", editor.CardPreviewNumber, StringComparison.Ordinal);

        editor.ToggleNumberCommand.Execute(null);
        Assert.True(editor.NumberVisible);
        Assert.False(editor.CvcVisible);
        editor.ToggleCvcCommand.Execute(null);
        Assert.True(editor.CvcVisible);
        editor.BeginEditCommand.Execute(null);
        Assert.False(editor.NumberVisible);
        Assert.False(editor.CvcVisible);
        Assert.Equal(ModalShellSize.Wide, editor.ModalSize);
    }

    [Fact]
    public void ApiKeyEditor_CancelEditPreservesSinglePrimaryField()
    {
        var entry = new ApiKeyEntry(
            "api-1",
            "Deploy key",
            "Provider",
            "Production",
            "notes",
            [new ApiKeyFieldEntry("field-1", "API Key", "API Key", "api-secret", true, true, 0)],
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z",
            "service-user");
        var editor = new ApiKeyEditorViewModel(
            _runtime,
            new StubApiKeyService(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

        editor.OpenDetails(new ApiKeyRowVm(entry, _localization));
        editor.BeginEditCommand.Execute(null);
        editor.Value = "changed-secret";
        editor.CancelEditCommand.Execute(null);

        Assert.Equal(ItemEditorMode.Details, editor.Mode);
        Assert.Equal("api-secret", editor.Value);
        Assert.False(editor.ValueVisible);
    }

    [Fact]
    public void ApiKeyEditor_DetailsMasksValueAndUsesRichSize()
    {
        var entry = new ApiKeyEntry(
            "api-1",
            "Deploy key",
            "Provider",
            "Production",
            "",
            [new ApiKeyFieldEntry("field-1", "API Key", "API Key", "api-secret", true, true, 0)],
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z",
            "service-user");
        var editor = new ApiKeyEditorViewModel(
            _runtime,
            new StubApiKeyService(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

        editor.OpenDetails(new ApiKeyRowVm(entry, _localization));

        Assert.Equal(ModalShellSize.ItemDetails, editor.ModalSize);
        Assert.DoesNotContain("api-secret", editor.ValueDisplay, StringComparison.Ordinal);
        Assert.Equal(_localization.Get("ItemWorkspace.Details.NoNotes"), editor.NotesDisplay);

        editor.ToggleValueCommand.Execute(null);
        editor.BeginDeleteCommand.Execute(null);
        Assert.False(editor.ValueVisible);
    }

    private WebLoginEditorViewModel CreateWebLoginEditor()
        => new(
            _runtime,
            new StubWebLoginService(),
            new CapturingPasswordGenerator(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

    private WebLoginRowVm CreateWebLoginRow()
        => new(
            _localization,
            "login-1",
            "Example",
            "user",
            "secret",
            "https://example.com/path",
            "original notes",
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z",
            false,
            "user@example.com");

    private CardRowVm CreateCardRow()
        => new(
            _localization,
            "card-1",
            "Personal",
            "Bank",
            "Owner",
            "4242424242424242",
            "09",
            "2030",
            "123",
            "notes",
            "Visa",
            "Credit Card",
            "2026-01-01T00:00:00Z",
            "2026-01-02T00:00:00Z");

    private sealed class StubActivityRecorder : IActivityRecorder
    {
        public ActivityLogService Store => throw new NotSupportedException();
        public event EventHandler? Changed { add { } remove { } }
        public void Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) { }
    }

    private sealed class StubSecureClipboard : ISecureClipboardService
    {
        public void Attach(IClipboard? clipboard) { }
        public Task CopyAsync(string text) => Task.CompletedTask;
        public Task ClearAsync() => Task.CompletedTask;
        public Task<Bitmap?> TryGetBitmapAsync() => Task.FromResult<Bitmap?>(null);
    }

    private sealed class CapturingPasswordGenerator : IPasswordGenerator
    {
        public PasswordGenerationOptions? LastOptions { get; private set; }

        public string? GeneratePassword(PasswordGenerationOptions options)
        {
            LastOptions = options;
            return "generated-password";
        }
    }

    private sealed class StubWebLoginService : IWebLoginService
    {
        public Task<IReadOnlyList<WebLoginEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WebLoginEntry>>([]);
        public Task<WebLoginEntry> AddAsync(string vaultPath, byte[] vaultKey, WebLoginInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WebLoginEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, WebLoginInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubCardService : ICardService
    {
        public Task<IReadOnlyList<CardEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardEntry>>([]);
        public Task<CardEntry> AddAsync(string vaultPath, byte[] vaultKey, CardInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CardEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, CardInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubApiKeyService : IApiKeyService
    {
        public Task<IReadOnlyList<ApiKeyEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApiKeyEntry>>([]);
        public Task<ApiKeyEntry> AddAsync(string vaultPath, byte[] vaultKey, ApiKeyInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiKeyEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, ApiKeyInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

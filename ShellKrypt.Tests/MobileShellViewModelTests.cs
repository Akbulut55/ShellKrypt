using ShellKrypt.Application.Items;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using ShellKrypt.Mobile.Security;
using ShellKrypt.Mobile.ViewModels;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class MobileShellViewModelTests
{
    private const string MasterPassword = "correct horse battery staple 2026!";

    [Fact]
    public void StartsOnWelcomePage()
    {
        using var workspace = new TempWorkspace();
        var vm = CreateVm(workspace.VaultPath);

        Assert.True(vm.IsWelcomeVisible);
        Assert.False(vm.ShowBottomNavigation);
        Assert.Equal("ShellKrypt", vm.PageTitle);
    }

    [Fact]
    public async Task CreateVaultCreatesAndUnlocksEmptyMobileVault()
    {
        using var workspace = new TempWorkspace();
        var vm = CreateVm(workspace.VaultPath);

        vm.CreateVaultCommand.Execute(null);
        vm.MasterPassword = MasterPassword;
        vm.ConfirmMasterPassword = MasterPassword;
        await vm.SaveNewVaultCommand.ExecuteAsync(null);

        Assert.True(File.Exists(workspace.VaultPath));
        Assert.True(vm.IsMainVisible);
        Assert.True(vm.ShowBottomNavigation);
        Assert.True(vm.ShowListEmptyState);
        Assert.Empty(vm.CurrentItems);
    }

    [Fact]
    public async Task UnlockExistingVaultLoadsRealItems()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var itemRepository = new SqliteItemRepository();
        var webService = new WebLoginService(itemRepository);

        await vaultService.CreateAsync(workspace.VaultPath, MasterPassword);
        var unlock = await vaultService.UnlockAsync(workspace.VaultPath, MasterPassword);
        Assert.True(unlock.Success);

        await webService.AddAsync(
            workspace.VaultPath,
            unlock.VaultKey!,
            new WebLoginInput(
                "GitHub",
                "https://github.com",
                "octo",
                "octo@example.com",
                "VeryStrongPassword123!",
                "primary login"));

        var vm = CreateVm(workspace.VaultPath);
        vm.ShowUnlockCommand.Execute(null);
        vm.MasterPassword = MasterPassword;
        await vm.UnlockCommand.ExecuteAsync(null);

        Assert.True(vm.IsMainVisible);
        Assert.Contains(vm.CurrentItems, item => item.Title == "GitHub");

        var webSection = vm.Sections.Single(section => section.Key == "web");
        vm.SelectSectionCommand.Execute(webSection);
        vm.SearchText = "github";

        var item = Assert.Single(vm.CurrentItems);
        Assert.Equal("GitHub", item.Title);
        Assert.Equal("web", item.SectionKey);
    }

    [Fact]
    public void ClipboardTimeoutIsClampedToMobileMinimum()
    {
        using var workspace = new TempWorkspace();
        var vm = CreateVm(workspace.VaultPath);
        vm.ClipboardClearSeconds = 1;

        Assert.Equal(MobileSecuritySettings.MinimumClipboardClearSeconds, vm.ClipboardClearSeconds);
        Assert.Equal(MobileSecuritySettings.MinimumClipboardClearSeconds, vm.CurrentSecuritySettings.ClipboardClearSeconds);
    }

    private static MobileShellViewModel CreateVm(string vaultPath)
    {
        var itemRepository = new SqliteItemRepository();
        return new MobileShellViewModel(
            new SqliteVaultService(),
            new VaultItemSummaryService(itemRepository, new VaultItemPayloadReader()),
            vaultPath);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKryptMobileTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            VaultPath = Path.Combine(Root, "mobile.skvault");
        }

        public string Root { get; }
        public string VaultPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}

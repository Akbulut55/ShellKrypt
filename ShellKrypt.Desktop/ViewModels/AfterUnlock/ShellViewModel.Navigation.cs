using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel
{
    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null)
            return;

        foreach (var item in NavItems)
            item.IsSelected = ReferenceEquals(item, value);

        CurrentPage = value.Key switch
        {
            ShellKryptSectionKeys.Vault => AllItems,
            ShellKryptSectionKeys.WebLogins => WebLogins,
            ShellKryptSectionKeys.Notes => MarkdownNotes,
            ShellKryptSectionKeys.Cards => Cards,
            ShellKryptSectionKeys.Generator => Tools,
            ShellKryptSectionKeys.Audit => Health,
            ShellKryptSectionKeys.Authenticator => Authenticator,
            ShellKryptSectionKeys.ApiKeys => ApiKeys,
            ShellKryptSectionKeys.Settings => Settings,
            ShellKryptSectionKeys.Activity => Activity,
            _ => AllItems
        };

        OnPropertyChanged(nameof(CurrentSectionTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(ShowAddItemAction));
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    [RelayCommand]
    private void Lock() => _root.Lock();

    [RelayCommand]
    private void SelectSection(NavItemVm? item)
    {
        if (item is not null)
            SelectedNav = item;
    }

    public void ShowAllItems()
    {
        SelectNav(ShellKryptSectionKeys.Vault);
    }

    public void ShowWebLogins() => SelectNav(ShellKryptSectionKeys.WebLogins);
    public async Task<bool> ShowWebLoginForRemediationAsync(string itemId, bool generateReplacementPassword = false)
    {
        SelectNav(ShellKryptSectionKeys.WebLogins);
        return await WebLogins.OpenForRemediationAsync(itemId, generateReplacementPassword);
    }

    public void ShowCards() => SelectNav(ShellKryptSectionKeys.Cards);
    public async Task<bool> ShowCardByIdAsync(string itemId)
    {
        SelectNav(ShellKryptSectionKeys.Cards);
        return await Cards.OpenEntryByIdAsync(itemId);
    }
    public void ShowMarkdownNotes() => SelectNav(ShellKryptSectionKeys.Notes);
    public void ShowSecurityAudit() => SelectNav(ShellKryptSectionKeys.Audit);
    public void ShowAuthenticator() => SelectNav(ShellKryptSectionKeys.Authenticator);
    public async Task<bool> ShowAuthenticatorByIdAsync(string itemId)
    {
        SelectNav(ShellKryptSectionKeys.Authenticator);
        return await Authenticator.OpenEntryByIdAsync(itemId);
    }
    public void ShowApiKeys() => SelectNav(ShellKryptSectionKeys.ApiKeys);
    public async Task<bool> ShowApiKeyByIdAsync(string itemId)
    {
        SelectNav(ShellKryptSectionKeys.ApiKeys);
        return await ApiKeys.OpenEntryByIdAsync(itemId);
    }
    public void ShowSettings() => SelectNav(ShellKryptSectionKeys.Settings);
    public void ShowActivity() => SelectNav(ShellKryptSectionKeys.Activity);

    private void SelectNav(string key)
    {
        foreach (var item in NavItems)
        {
            if (item.Key == key)
            {
                SelectedNav = item;
                return;
            }
        }
    }
}

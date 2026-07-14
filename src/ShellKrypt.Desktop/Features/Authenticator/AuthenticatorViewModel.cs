using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.Authenticator;

public partial class AuthenticatorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IAuthenticatorEntryService _entryService;
    private readonly IOneTimePasswordGenerator _codeGenerator;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly IAuthenticatorRefreshTimer _refreshTimer;
    private readonly List<AuthenticatorAccountVm> _allEntries = new();

    public ObservableCollection<AuthenticatorAccountVm> FilteredEntries { get; } = new();

    [ObservableProperty] private AuthenticatorAccountVm? selectedEntry;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string error = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDeleteConfirmOpen;

    public AuthenticatorViewModel(
        MainWindowViewModel root,
        IAuthenticatorEntryService entryService,
        IOneTimePasswordGenerator codeGenerator,
        AuthenticatorQrImageImportService qrImportService,
        IAuthenticatorRefreshTimer refreshTimer,
        Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _entryService = entryService;
        _codeGenerator = codeGenerator;
        _refreshTimer = refreshTimer;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        Editor = new AuthenticatorEditorViewModel(root, qrImportService)
        {
            SaveRequested = SaveEditorAsync
        };

        _refreshTimer.Tick += (_, _) => RefreshSnapshots();

        _ = LoadAsync();
    }

    public AuthenticatorEditorViewModel Editor { get; }

    public void Activate()
    {
        RefreshSnapshots();
        if (!_refreshTimer.IsRunning)
            _refreshTimer.Start();
    }

    public void Deactivate()
    {
        _refreshTimer.Stop();
        Editor.Close();
        foreach (var entry in _allEntries)
            entry.IsSecretVisible = false;
    }

    public int TotalCount => _allEntries.Count;
    public string CodesCountDisplay => T(_root, "Authenticator.Codes.Count", TotalCount);
    public bool HasEntries => FilteredEntries.Count > 0;
    public bool HasSelection => SelectedEntry is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanEditSelection => SelectedEntry is not null && !IsBusy;
    public bool CanCopyCode => SelectedEntry?.IsCodeValid == true && !IsBusy;
    public string PageSubtitle => T(_root, "Authenticator.Subtitle");
    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Authenticator.Empty.NoneTitle")
        : T(_root, "Authenticator.Empty.NoMatchTitle");
    public string EmptySubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Authenticator.Empty.NoneSubtitle")
        : T(_root, "Authenticator.Empty.NoMatchSubtitle");
    public string DetailSubtitle => SelectedEntry is null
        ? T(_root, "Authenticator.Detail.SelectCode")
        : SelectedEntry.KeyTypeDisplay;
    public string DeleteConfirmationText => SelectedEntry is null
        ? T(_root, "Authenticator.Delete.TitleFallback")
        : T(_root, "Authenticator.Delete.Title", SelectedEntry.Name);
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedEntryChanged(AuthenticatorAccountVm? value)
    {
        foreach (var entry in FilteredEntries)
            entry.IsSelected = ReferenceEquals(entry, value);

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(CanCopyCode));
        OnPropertyChanged(nameof(DetailSubtitle));
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(CanCopyCode));
    }

    public override void RefreshLocalization()
    {
        foreach (var entry in _allEntries)
            entry.RefreshLocalization();
        Editor.RefreshLocalization();

        NotifyLocalized(
            nameof(PageSubtitle),
            nameof(CodesCountDisplay),
            nameof(EmptyTitle),
            nameof(EmptySubtitle),
            nameof(DetailSubtitle),
            nameof(DeleteConfirmationText));
    }
}

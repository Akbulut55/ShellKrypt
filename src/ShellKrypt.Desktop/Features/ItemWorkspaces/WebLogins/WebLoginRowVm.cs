using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using System;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;

public sealed partial class WebLoginRowVm : ObservableObject
{
    private readonly LocalizationService _localization;

    public string Id { get; }
    public bool IsNew { get; private set; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string username;
    [ObservableProperty] private string email;
    [ObservableProperty] private string password;
    [ObservableProperty] private string url;
    [ObservableProperty] private string notes;
    [ObservableProperty] private bool isPasswordVisible;

    public WebLoginRowVm(
        LocalizationService localization,
        string id,
        string title,
        string username,
        string password,
        string url,
        string notes,
        string createdAtUtc,
        string updatedAtUtc,
        bool isNew,
        string email = "")
    {
        _localization = localization;
        Id = id;
        Title = title ?? "";
        Username = username ?? "";
        Email = email ?? "";
        Password = password ?? "";
        Url = url ?? "";
        Notes = notes ?? "";
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsNew = isNew;
    }

    public string IconLetter => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[0].ToString().ToUpperInvariant();
    public string UsernameDisplay => string.IsNullOrWhiteSpace(Username) ? Email : Username;
    public string PasswordDisplay => IsPasswordVisible ? Password : "**********";
    public string UrlHost => FormatUrlHost(Url);

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnUsernameChanged(string value) => OnPropertyChanged(nameof(UsernameDisplay));
    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(UsernameDisplay));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(PasswordDisplay));
    partial void OnUrlChanged(string value) => OnPropertyChanged(nameof(UrlHost));
    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(PasswordDisplay));

    public void MarkSaved(string updatedAtUtc)
    {
        IsNew = false;
        UpdatedAtUtc = string.IsNullOrWhiteSpace(updatedAtUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : updatedAtUtc;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(UrlHost));
    }

    private string FormatUrlHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return _localization.Get("WebLogins.Row.NoUrl");

        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Host))
            return absolute.Host;

        var withoutScheme = text
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase);

        var slash = withoutScheme.IndexOf('/');
        return slash < 0 ? withoutScheme : withoutScheme[..slash];
    }
}

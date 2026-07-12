using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using ShellKrypt.Application.Localization;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;

public sealed partial class ApiKeyFieldRowVm : ObservableObject
{
    public string Id { get; }
    public int SortOrder { get; set; }

    [ObservableProperty] private string label;
    [ObservableProperty] private string fieldType;
    [ObservableProperty] private string value;
    [ObservableProperty] private bool isSensitive;
    [ObservableProperty] private bool isCopyable;
    [ObservableProperty] private bool isValueVisible;

    public ApiKeyFieldRowVm(
        string id,
        string label,
        string fieldType,
        string value,
        bool isSensitive,
        bool isCopyable,
        int sortOrder)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Label = label ?? "";
        FieldType = string.IsNullOrWhiteSpace(fieldType) ? ApiKeysViewModel.DefaultFieldType : fieldType;
        Value = value ?? "";
        IsSensitive = isSensitive;
        IsCopyable = isCopyable;
        SortOrder = sortOrder;
    }

    public string DisplayValue => IsSensitive && !IsValueVisible ? MaskValue(Value) : Value;

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    partial void OnIsSensitiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    partial void OnIsValueVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    private static string MaskValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var visibleTail = value.Length <= 4 ? "" : value[^4..];
        return string.IsNullOrWhiteSpace(visibleTail)
            ? "****"
            : $"**** **** {visibleTail}";
    }

}

public sealed partial class ApiKeyRowVm : ObservableObject
{
    private readonly LocalizationService _localization;

    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string provider;
    [ObservableProperty] private string user;
    [ObservableProperty] private string environment;
    [ObservableProperty] private string notes;

    public ObservableCollection<ApiKeyFieldRowVm> Fields { get; } = new();

    public ApiKeyRowVm(ApiKeyEntry entry, LocalizationService localization)
    {
        _localization = localization;
        Id = entry.Id;
        CreatedAtUtc = entry.CreatedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        Name = entry.Name;
        Provider = entry.Provider;
        User = entry.User;
        Environment = entry.Environment;
        Notes = entry.Notes;

        foreach (var field in entry.Fields.OrderBy(field => field.SortOrder))
        {
            Fields.Add(new ApiKeyFieldRowVm(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder));
        }
    }

    public string Monogram
    {
        get
        {
            var letters = Name.Where(char.IsLetterOrDigit).Take(2).ToArray();
            return letters.Length == 0 ? "AK" : new string(letters).ToUpperInvariant();
        }
    }

    public string ProviderDisplay => string.IsNullOrWhiteSpace(Provider) ? T("ApiKeys.Field.UnknownProvider") : Provider.Trim();
    public string UserDisplay => string.IsNullOrWhiteSpace(User) ? T("ApiKeys.Field.NoUser") : User.Trim();
    public ApiKeyFieldRowVm? PrimaryField => Fields.FirstOrDefault(apiField => apiField.IsSensitive && apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault(apiField => apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault();
    public string PrimaryFieldLabel => PrimaryField?.Label ?? T("ApiKeys.Field.NoField");
    public string PrimaryFieldDisplay => PrimaryField?.DisplayValue ?? T("ApiKeys.Field.NoKeyStored");
    public string PrimaryCopyValue => PrimaryField?.Value ?? "";

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Monogram));
    }

    partial void OnProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ProviderDisplay));
    }

    partial void OnUserChanged(string value)
    {
        OnPropertyChanged(nameof(UserDisplay));
    }

    public void Apply(ApiKeyEntry entry)
    {
        Name = entry.Name;
        Provider = entry.Provider;
        User = entry.User;
        Environment = entry.Environment;
        Notes = entry.Notes;
        UpdatedAtUtc = entry.UpdatedAtUtc;

        Fields.Clear();
        foreach (var field in entry.Fields.OrderBy(field => field.SortOrder))
        {
            Fields.Add(new ApiKeyFieldRowVm(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder));
        }

        NotifyFieldsChanged();
    }

    public void NotifyFieldsChanged()
    {
        OnPropertyChanged(nameof(PrimaryField));
        OnPropertyChanged(nameof(PrimaryFieldLabel));
        OnPropertyChanged(nameof(PrimaryFieldDisplay));
        OnPropertyChanged(nameof(PrimaryCopyValue));
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(ProviderDisplay));
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(PrimaryFieldLabel));
        OnPropertyChanged(nameof(PrimaryFieldDisplay));
    }

    private string T(string key, params object[] args) => _localization.Get(key, args);
}

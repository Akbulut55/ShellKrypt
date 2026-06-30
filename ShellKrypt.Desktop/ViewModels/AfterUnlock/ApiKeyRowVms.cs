using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using ShellKrypt.Application.Localization;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ApiKeyFieldRowVm : ObservableObject
{
    private readonly LocalizationService _localization;

    public string Id { get; }
    public int SortOrder { get; set; }

    [ObservableProperty] private string label;
    [ObservableProperty] private string fieldType;
    [ObservableProperty] private string value;
    [ObservableProperty] private bool isSensitive;
    [ObservableProperty] private bool isCopyable;
    [ObservableProperty] private bool isValueVisible;

    public ApiKeyFieldRowVm(
        LocalizationService localization,
        string id,
        string label,
        string fieldType,
        string value,
        bool isSensitive,
        bool isCopyable,
        int sortOrder)
    {
        _localization = localization;
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Label = label ?? "";
        FieldType = string.IsNullOrWhiteSpace(fieldType) ? ApiKeysViewModel.DefaultFieldType : fieldType;
        Value = value ?? "";
        IsSensitive = isSensitive;
        IsCopyable = isCopyable;
        SortOrder = sortOrder;
    }

    public string DisplayValue => IsSensitive && !IsValueVisible ? MaskValue(Value) : Value;
    public string VisibilityLabel => IsValueVisible ? T("ApiKeys.Field.Hide") : T("ApiKeys.Field.Show");
    public string CopyLabel => IsCopyable ? T("ApiKeys.Field.Copy") : T("ApiKeys.Field.Locked");
    public bool UseMaskedValueInput => IsSensitive && !IsValueVisible;
    public bool UsePlainValueInput => !UseMaskedValueInput;
    public bool CanReveal => IsSensitive;

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    partial void OnIsSensitiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(UseMaskedValueInput));
        OnPropertyChanged(nameof(UsePlainValueInput));
        OnPropertyChanged(nameof(CanReveal));
    }

    partial void OnIsCopyableChanged(bool value) => OnPropertyChanged(nameof(CopyLabel));
    partial void OnIsValueVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(VisibilityLabel));
        OnPropertyChanged(nameof(UseMaskedValueInput));
        OnPropertyChanged(nameof(UsePlainValueInput));
    }

    public ApiKeyFieldRowVm Clone()
        => new(_localization, Id, Label, FieldType, Value, IsSensitive, IsCopyable, SortOrder)
        {
            IsValueVisible = IsValueVisible
        };

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(VisibilityLabel));
        OnPropertyChanged(nameof(CopyLabel));
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

    private string T(string key, params object[] args) => _localization.Get(key, args);
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
                _localization,
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
    public bool HasUser => !string.IsNullOrWhiteSpace(User);
    public string EnvironmentDisplay => string.IsNullOrWhiteSpace(Environment) ? T("ApiKeys.Environment.Default") : Environment.Trim();
    public string FieldCountDisplay => HasUser ? UserDisplay : ProviderDisplay;
    public ApiKeyFieldRowVm? PrimaryField => Fields.FirstOrDefault(apiField => apiField.IsSensitive && apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault(apiField => apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault();
    public string PrimaryFieldLabel => PrimaryField?.Label ?? T("ApiKeys.Field.NoField");
    public string PrimaryFieldDisplay => PrimaryField?.DisplayValue ?? T("ApiKeys.Field.NoKeyStored");
    public string PrimaryCopyValue => PrimaryField?.Value ?? "";
    public string UpdatedDisplay => FormatRelativeDate(UpdatedAtUtc);
    public string SearchText => string.Join(" ", new[]
    {
        Name,
        Provider,
        User,
        Environment,
        Notes,
        string.Join(" ", Fields.Select(apiField => $"{apiField.Label} {apiField.FieldType} {apiField.Value}"))
    }.Where(part => !string.IsNullOrWhiteSpace(part)));

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Monogram));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ProviderDisplay));
        OnPropertyChanged(nameof(FieldCountDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnUserChanged(string value)
    {
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(HasUser));
        OnPropertyChanged(nameof(FieldCountDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnEnvironmentChanged(string value)
    {
        OnPropertyChanged(nameof(EnvironmentDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(SearchText));

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
                _localization,
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder));
        }

        NotifyFieldsChanged();
        OnPropertyChanged(nameof(UpdatedDisplay));
    }

    public void NotifyFieldsChanged()
    {
        OnPropertyChanged(nameof(FieldCountDisplay));
        OnPropertyChanged(nameof(PrimaryField));
        OnPropertyChanged(nameof(PrimaryFieldLabel));
        OnPropertyChanged(nameof(PrimaryFieldDisplay));
        OnPropertyChanged(nameof(PrimaryCopyValue));
        OnPropertyChanged(nameof(SearchText));
    }

    public void RefreshLocalization()
    {
        foreach (var field in Fields)
            field.RefreshLocalization();

        OnPropertyChanged(nameof(ProviderDisplay));
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(HasUser));
        OnPropertyChanged(nameof(FieldCountDisplay));
        OnPropertyChanged(nameof(PrimaryFieldLabel));
        OnPropertyChanged(nameof(PrimaryFieldDisplay));
        OnPropertyChanged(nameof(UpdatedDisplay));
    }

    private string FormatRelativeDate(string? value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return T("ApiKeys.Time.Unknown");

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.Zero)
            return local.ToString("MMM d", CultureInfo.InvariantCulture);
        if (delta < TimeSpan.FromMinutes(1))
            return T("ApiKeys.Time.JustNow");
        if (delta < TimeSpan.FromHours(1))
            return T("ApiKeys.Time.MinutesAgo", Math.Max(1, (int)delta.TotalMinutes));
        if (delta < TimeSpan.FromDays(1))
            return T("ApiKeys.Time.HoursAgo", Math.Max(1, (int)delta.TotalHours));
        if (delta < TimeSpan.FromDays(7))
            return T("ApiKeys.Time.DaysAgo", Math.Max(1, (int)delta.TotalDays));

        return local.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private string T(string key, params object[] args) => _localization.Get(key, args);
}

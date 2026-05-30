using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

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
    public string VisibilityLabel => IsValueVisible ? "Hide" : "Show";
    public string CopyLabel => IsCopyable ? "Copy" : "Locked";
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
        => new(Id, Label, FieldType, Value, IsSensitive, IsCopyable, SortOrder)
        {
            IsValueVisible = IsValueVisible
        };

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
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string provider;
    [ObservableProperty] private string environment;
    [ObservableProperty] private string notes;

    public ObservableCollection<ApiKeyFieldRowVm> Fields { get; } = new();

    public ApiKeyRowVm(ApiKeyEntry entry)
    {
        Id = entry.Id;
        CreatedAtUtc = entry.CreatedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        Name = entry.Name;
        Provider = entry.Provider;
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

    public string ProviderDisplay => string.IsNullOrWhiteSpace(Provider) ? "Unknown provider" : Provider.Trim();
    public string EnvironmentDisplay => string.IsNullOrWhiteSpace(Environment) ? "Production" : Environment.Trim();
    public string FieldCountDisplay => Fields.Count == 1 ? "1 field" : $"{Fields.Count} fields";
    public ApiKeyFieldRowVm? PrimaryField => Fields.FirstOrDefault(apiField => apiField.IsSensitive && apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault(apiField => apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault();
    public string PrimaryFieldLabel => PrimaryField?.Label ?? "No field";
    public string PrimaryFieldDisplay => PrimaryField?.DisplayValue ?? "No key stored";
    public string PrimaryCopyValue => PrimaryField?.Value ?? "";
    public string UpdatedDisplay => FormatRelativeDate(UpdatedAtUtc);
    public string SearchText => string.Join(" ", new[]
    {
        Name,
        Provider,
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

    private static string FormatRelativeDate(string? value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.Zero)
            return local.ToString("MMM d", CultureInfo.InvariantCulture);
        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return local.ToString("MMM d", CultureInfo.InvariantCulture);
    }
}

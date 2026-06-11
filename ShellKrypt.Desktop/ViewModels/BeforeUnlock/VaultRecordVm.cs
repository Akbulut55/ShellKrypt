using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class VaultRecordVm : ObservableObject
{
    private readonly LocalizationService _localization;

    public string Id { get; }
    public string VaultPath { get; }
    public string CreatedAtUtc { get; }

    [ObservableProperty] private string displayName;
    [ObservableProperty] private string description;
    [ObservableProperty] private string? lastOpenedAtUtc;
    [ObservableProperty] private bool isDefault;

    public VaultRecordVm(VaultRegistryEntry entry, LocalizationService localization)
    {
        _localization = localization;
        Id = entry.Id;
        VaultPath = entry.VaultPath;
        CreatedAtUtc = entry.CreatedAtUtc;
        DisplayName = entry.DisplayName;
        Description = entry.Description;
        LastOpenedAtUtc = entry.LastOpenedAtUtc;
        IsDefault = entry.IsDefault;
    }

    public string FileName => Path.GetFileName(VaultPath);
    public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? FileName : DisplayName;
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? T("Welcome.Vault.NoDescription") : Description;
    public string PathDisplay => VaultPath;
    public string CreatedDisplay => FormatDate(CreatedAtUtc);
    public string LastOpenedDisplay => FormatDate(LastOpenedAtUtc);
    public string LastOpenedDisplayLabel => T("Welcome.Vault.LastAccessed", LastOpenedDisplay);
    public string StatusDisplay => Exists ? T("Welcome.Vault.Available") : T("Welcome.Vault.Missing");
    public string AvailabilityBadge => Exists ? T("Welcome.Vault.Available") : T("Welcome.Vault.Missing");
    public bool Exists => File.Exists(VaultPath);
    public string DefaultBadge => IsDefault ? T("Common.Default") : T("Welcome.Vault.Label");
    public bool CanBeDefault => Exists && !IsDefault;

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(DescriptionDisplay));
    partial void OnLastOpenedAtUtcChanged(string? value)
    {
        OnPropertyChanged(nameof(LastOpenedDisplay));
        OnPropertyChanged(nameof(LastOpenedDisplayLabel));
    }
    partial void OnIsDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(DefaultBadge));
        OnPropertyChanged(nameof(CanBeDefault));
    }

    public VaultRegistryEntry ToEntry()
        => new()
        {
            Id = Id,
            VaultPath = VaultPath,
            DisplayName = DisplayName,
            Description = Description,
            CreatedAtUtc = CreatedAtUtc,
            LastOpenedAtUtc = LastOpenedAtUtc,
            IsDefault = IsDefault
        };

    public void Apply(VaultRegistryEntry entry)
    {
        DisplayName = entry.DisplayName;
        Description = entry.Description;
        LastOpenedAtUtc = entry.LastOpenedAtUtc;
        IsDefault = entry.IsDefault;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DescriptionDisplay));
        OnPropertyChanged(nameof(CreatedDisplay));
        OnPropertyChanged(nameof(LastOpenedDisplay));
        OnPropertyChanged(nameof(LastOpenedDisplayLabel));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(AvailabilityBadge));
        OnPropertyChanged(nameof(DefaultBadge));
    }

    private string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return T("Common.Never");

        return DateTimeOffset.TryParse(value, out var dto)
            ? dto.LocalDateTime.ToString("g")
            : value;
    }

    private string T(string key, params object[] args) => _localization.Get(key, args);
}

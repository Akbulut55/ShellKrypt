using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class VaultRecordVm : ObservableObject
{
    public string Id { get; }
    public string VaultPath { get; }
    public string CreatedAtUtc { get; }

    [ObservableProperty] private string displayName;
    [ObservableProperty] private string description;
    [ObservableProperty] private string? lastOpenedAtUtc;
    [ObservableProperty] private bool isDefault;

    public VaultRecordVm(VaultRegistryEntry entry)
    {
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
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "No description" : Description;
    public string PathDisplay => VaultPath;
    public string CreatedDisplay => FormatDate(CreatedAtUtc);
    public string LastOpenedDisplay => FormatDate(LastOpenedAtUtc);
    public string StatusDisplay => Exists ? "Available" : "Missing";
    public bool Exists => File.Exists(VaultPath);
    public string DefaultBadge => IsDefault ? "Default" : "Vault";

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(DescriptionDisplay));
    partial void OnLastOpenedAtUtcChanged(string? value) => OnPropertyChanged(nameof(LastOpenedDisplay));
    partial void OnIsDefaultChanged(bool value) => OnPropertyChanged(nameof(DefaultBadge));

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

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Never";

        return DateTimeOffset.TryParse(value, out var dto)
            ? dto.LocalDateTime.ToString("g")
            : value;
    }
}

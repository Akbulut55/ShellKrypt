using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AuthenticatorAccountVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string secret;
    [ObservableProperty] private AuthenticatorKeyType keyType;
    [ObservableProperty] private long counter;
    [ObservableProperty] private string algorithm;
    [ObservableProperty] private int digits;
    [ObservableProperty] private int periodSeconds;
    [ObservableProperty] private string lastUsedAtUtc;
    [ObservableProperty] private string currentCodeRaw;
    [ObservableProperty] private int secondsRemaining;
    [ObservableProperty] private double progressPercent;
    [ObservableProperty] private bool isCodeValid;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isSecretVisible;

    public AuthenticatorAccountVm(AuthenticatorEntry entry)
    {
        Id = entry.Id;
        CreatedAtUtc = entry.CreatedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        Name = entry.Name;
        Secret = entry.Secret;
        KeyType = entry.KeyType;
        Counter = entry.Counter;
        Algorithm = entry.Algorithm;
        Digits = entry.Digits;
        PeriodSeconds = entry.PeriodSeconds;
        LastUsedAtUtc = entry.LastUsedAtUtc;
        CurrentCodeRaw = "------";
        SecondsRemaining = 0;
        ProgressPercent = 0;
        IsCodeValid = false;
    }

    public string Monogram
    {
        get
        {
            var letters = Name
                .Where(char.IsLetterOrDigit)
                .Take(2)
                .ToArray();

            return letters.Length == 0
                ? "AU"
                : new string(letters).ToUpperInvariant();
        }
    }

    public string AccountSubtitle => KeyType == AuthenticatorKeyType.CounterBased
        ? "Counter based code"
        : "Time based code";

    public string CurrentCodeDisplay => FormatCode(CurrentCodeRaw);
    public string RemainingDisplay => SecondsRemaining <= 0 ? "0:00" : $"0:{SecondsRemaining:00}";
    public string SecretDisplay => IsSecretVisible ? FormatSecret(Secret) : "**** **** **** ****";
    public string DigitsDisplay => $"{Digits} digits";
    public string LastUsedDisplay => FormatRelativeTimestamp(LastUsedAtUtc);
    public string VerifiedLabel => IsCodeValid ? "Ready" : "Invalid";
    public string KeyTypeDisplay => KeyType == AuthenticatorKeyType.CounterBased ? "Counter Based" : "Time Based";
    public string AlgorithmDisplay => NormalizeAlgorithmLabel(Algorithm);
    public string RotationDisplay => KeyType == AuthenticatorKeyType.TimeBased
        ? $"{AlgorithmDisplay} \u00C2\u00B7 {PeriodSeconds}s"
        : $"{AlgorithmDisplay} \u00C2\u00B7 Counter";
    public string CounterDisplay => Counter.ToString(CultureInfo.InvariantCulture);
    public bool HasCountdown => KeyType == AuthenticatorKeyType.TimeBased;
    public string ProgressLabel => KeyType == AuthenticatorKeyType.TimeBased
        ? $"{PeriodSeconds}s rotation"
        : $"Counter {Counter}";
    public string DetailHint => KeyType == AuthenticatorKeyType.TimeBased
        ? "This code rotates automatically."
        : "This code advances when you use it.";
    public string CopyButtonText => KeyType == AuthenticatorKeyType.CounterBased ? "Copy Code & Advance" : "Copy Code";

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Monogram));
    partial void OnCurrentCodeRawChanged(string value) => OnPropertyChanged(nameof(CurrentCodeDisplay));
    partial void OnSecondsRemainingChanged(int value) => OnPropertyChanged(nameof(RemainingDisplay));
    partial void OnSecretChanged(string value) => OnPropertyChanged(nameof(SecretDisplay));
    partial void OnLastUsedAtUtcChanged(string value) => OnPropertyChanged(nameof(LastUsedDisplay));
    partial void OnIsSecretVisibleChanged(bool value) => OnPropertyChanged(nameof(SecretDisplay));

    partial void OnKeyTypeChanged(AuthenticatorKeyType value)
    {
        OnPropertyChanged(nameof(AccountSubtitle));
        OnPropertyChanged(nameof(KeyTypeDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(DetailHint));
        OnPropertyChanged(nameof(CopyButtonText));
    }

    partial void OnCounterChanged(long value)
    {
        OnPropertyChanged(nameof(CounterDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(ProgressLabel));
    }

    partial void OnPeriodSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(RotationDisplay));
        OnPropertyChanged(nameof(ProgressLabel));
    }

    partial void OnAlgorithmChanged(string value)
    {
        OnPropertyChanged(nameof(AlgorithmDisplay));
        OnPropertyChanged(nameof(RotationDisplay));
    }

    public void Apply(AuthenticatorEntry entry)
    {
        Name = entry.Name;
        Secret = entry.Secret;
        KeyType = entry.KeyType;
        Counter = entry.Counter;
        Algorithm = entry.Algorithm;
        Digits = entry.Digits;
        PeriodSeconds = entry.PeriodSeconds;
        LastUsedAtUtc = entry.LastUsedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
    }

    public void ApplySnapshot(AuthenticatorCodeSnapshot snapshot)
    {
        CurrentCodeRaw = snapshot.Code;
        SecondsRemaining = snapshot.SecondsRemaining;
        ProgressPercent = snapshot.ProgressPercent;
        IsCodeValid = snapshot.IsValid;
    }

    public AuthenticatorEntry ToEntry()
        => new(
            Id,
            Name,
            Secret,
            KeyType,
            Counter,
            Algorithm,
            Digits,
            PeriodSeconds,
            LastUsedAtUtc,
            CreatedAtUtc,
            UpdatedAtUtc);

    private static string FormatCode(string? rawCode)
    {
        var value = (rawCode ?? string.Empty).Trim();
        if (value.Length == 6)
            return $"{value[..3]} {value[3..]}";
        if (value.Length == 8)
            return $"{value[..4]} {value[4..]}";

        return value;
    }

    private static string FormatSecret(string? secret)
    {
        var normalized = new string((secret ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (normalized.Length == 0)
            return string.Empty;

        var groups = new List<string>();
        for (var index = 0; index < normalized.Length; index += 4)
            groups.Add(normalized.Substring(index, Math.Min(4, normalized.Length - index)));

        return string.Join(" ", groups);
    }

    private static string FormatRelativeTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Never used";

        if (!DateTimeOffset.TryParse(value, out var timestamp))
            return "Unknown";

        var delta = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
        if (delta.TotalMinutes < 1)
            return "Just now";
        if (delta.TotalHours < 1)
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta.TotalDays < 1)
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta.TotalDays < 7)
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return timestamp.ToLocalTime().ToString("MMM dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeAlgorithmLabel(string? algorithm)
        => (algorithm ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "HMAC-SHA1" => "SHA1",
            "HMAC-SHA256" => "SHA256",
            "HMAC-SHA512" => "SHA512",
            _ => "SHA1"
        };
}

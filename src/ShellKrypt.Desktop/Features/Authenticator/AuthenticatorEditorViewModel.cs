using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.Authenticator;

public sealed partial class AuthenticatorEditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly AuthenticatorQrImageImportService _qrImportService;
    private long _counter;

    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private bool isEditingExisting;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSecretVisible;
    [ObservableProperty] private bool isAdvancedOptionsExpanded;
    [ObservableProperty] private string error = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string secret = string.Empty;
    [ObservableProperty] private string periodSecondsText = "30";
    [ObservableProperty] private AuthenticatorKeyTypeOption? selectedKeyType;
    [ObservableProperty] private AuthenticatorAlgorithmOption? selectedAlgorithm;
    [ObservableProperty] private AuthenticatorDigitsOption? selectedDigits;

    public AuthenticatorEditorViewModel(MainWindowViewModel root, AuthenticatorQrImageImportService qrImportService)
    {
        _root = root;
        _qrImportService = qrImportService;
        SelectedKeyType = KeyTypeOptions[0];
        SelectedAlgorithm = AlgorithmOptions[0];
        SelectedDigits = DigitsOptions[0];
    }

    public Func<AuthenticatorInput, AuthenticatorAccountVm?, Task>? SaveRequested { get; set; }
    public AuthenticatorAccountVm? ExistingEntry { get; private set; }

    public IReadOnlyList<AuthenticatorKeyTypeOption> KeyTypeOptions { get; } =
    [
        new(AuthenticatorKeyType.TimeBased, "Time Based"),
        new(AuthenticatorKeyType.CounterBased, "Counter Based")
    ];

    public IReadOnlyList<AuthenticatorAlgorithmOption> AlgorithmOptions { get; } =
    [
        new("HMAC-SHA1", "SHA1 algorithm (Default)", "SHA1"),
        new("HMAC-SHA256", "SHA256 algorithm", "SHA256"),
        new("HMAC-SHA512", "SHA512 algorithm", "SHA512")
    ];

    public IReadOnlyList<AuthenticatorDigitsOption> DigitsOptions { get; } =
    [
        new(6, "6 digits (Default)"),
        new(8, "8 digits")
    ];

    public string Title => IsEditingExisting ? T(_root, "Authenticator.Modal.EditTitle") : T(_root, "Authenticator.Modal.AddTitle");
    public string Subtitle => T(_root, "Authenticator.Modal.Subtitle");
    public string AdvancedOptionsNote => T(_root, "Authenticator.Advanced.Note");
    public string SaveButtonText => IsEditingExisting ? T(_root, "Common.SaveChanges") : T(_root, "Authenticator.Button.AddCode");
    public string TypeSummary => SelectedKeyType?.KeyType == AuthenticatorKeyType.CounterBased
        ? T(_root, "Authenticator.TypeSummary.Counter", _counter, SelectedAlgorithm?.ShortLabel ?? "SHA1", SelectedDigits?.Digits ?? 6)
        : T(_root, "Authenticator.TypeSummary.Time", NormalizePeriodText(PeriodSecondsText), SelectedAlgorithm?.ShortLabel ?? "SHA1", SelectedDigits?.Digits ?? 6);
    public bool ShowPeriod => SelectedKeyType?.KeyType == AuthenticatorKeyType.TimeBased;

    public void OpenAdd()
    {
        ExistingEntry = null;
        IsEditingExisting = false;
        Name = string.Empty;
        Secret = string.Empty;
        SelectedKeyType = KeyTypeOptions[0];
        SelectedAlgorithm = AlgorithmOptions[0];
        SelectedDigits = DigitsOptions[0];
        PeriodSecondsText = "30";
        _counter = 0;
        Open();
    }

    public void OpenEdit(AuthenticatorAccountVm entry)
    {
        ExistingEntry = entry;
        IsEditingExisting = true;
        Name = entry.Name;
        Secret = entry.Secret;
        SelectedKeyType = KeyTypeOptions.First(option => option.KeyType == entry.KeyType);
        SelectedAlgorithm = ResolveAlgorithm(entry.Algorithm);
        SelectedDigits = DigitsOptions.First(option => option.Digits == (entry.Digits == 8 ? 8 : 6));
        PeriodSecondsText = entry.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        _counter = entry.Counter;
        Open();
    }

    public void Close()
    {
        Error = string.Empty;
        IsSecretVisible = false;
        IsOpen = false;
    }

    public override void RefreshLocalization()
        => NotifyLocalized(nameof(Title), nameof(Subtitle), nameof(AdvancedOptionsNote), nameof(SaveButtonText), nameof(TypeSummary));

    partial void OnIsEditingExistingChanged(bool value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveButtonText));
    }
    partial void OnSelectedKeyTypeChanged(AuthenticatorKeyTypeOption? value)
    {
        OnPropertyChanged(nameof(TypeSummary));
        OnPropertyChanged(nameof(ShowPeriod));
    }
    partial void OnSelectedAlgorithmChanged(AuthenticatorAlgorithmOption? value) => OnPropertyChanged(nameof(TypeSummary));
    partial void OnSelectedDigitsChanged(AuthenticatorDigitsOption? value) => OnPropertyChanged(nameof(TypeSummary));
    partial void OnPeriodSecondsTextChanged(string value) => OnPropertyChanged(nameof(TypeSummary));

    [RelayCommand]
    private void Cancel() => Close();

    [RelayCommand]
    private void ToggleSecretVisibility() => IsSecretVisible = !IsSecretVisible;

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = T(_root, "Authenticator.Validation.NameRequired");
            return;
        }
        if (string.IsNullOrWhiteSpace(Secret))
        {
            Error = T(_root, "Authenticator.Validation.SecretRequired");
            return;
        }
        if (SelectedKeyType is null)
        {
            Error = T(_root, "Authenticator.Validation.KeyTypeRequired");
            return;
        }

        IsBusy = true;
        try
        {
            var input = new AuthenticatorInput(
                Name,
                Secret,
                SelectedKeyType.KeyType,
                SelectedKeyType.KeyType == AuthenticatorKeyType.CounterBased ? _counter : 0,
                SelectedAlgorithm?.Value ?? "HMAC-SHA1",
                SelectedDigits?.Digits ?? 6,
                ResolvePeriod());
            if (SaveRequested is not null)
                await SaveRequested(input, ExistingEntry);
            Close();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportQrScreenshotAsync()
    {
        Error = string.Empty;
        var path = await _root.PickOpenFileAsync(
            T(_root, "Authenticator.Qr.PickerTitle"),
            [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"],
            T(_root, "Authenticator.Qr.FileType"));
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            ApplyImportedSecret(_qrImportService.ImportFromImage(path));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PasteQrImageAsync()
    {
        Error = string.Empty;
        try
        {
            using var bitmap = await _root.TryGetClipboardBitmapAsync();
            if (bitmap is null)
            {
                Error = T(_root, "Authenticator.Qr.NoClipboardImage");
                return;
            }
            ApplyImportedSecret(_qrImportService.ImportFromBitmap(bitmap));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void Open()
    {
        Error = string.Empty;
        IsSecretVisible = false;
        IsAdvancedOptionsExpanded = false;
        IsOpen = true;
        OnPropertyChanged(nameof(TypeSummary));
    }

    private void ApplyImportedSecret(ParsedOtpAuthSecret parsed)
    {
        Name = parsed.Name;
        Secret = parsed.Secret;
        SelectedKeyType = KeyTypeOptions.First(option => option.KeyType == parsed.KeyType);
        SelectedAlgorithm = ResolveAlgorithm(parsed.Algorithm);
        SelectedDigits = DigitsOptions.First(option => option.Digits == (parsed.Digits == 8 ? 8 : 6));
        PeriodSecondsText = parsed.PeriodSeconds.ToString(CultureInfo.InvariantCulture);
        _counter = parsed.Counter;
        IsSecretVisible = false;
        IsOpen = true;
        OnPropertyChanged(nameof(TypeSummary));
    }

    private AuthenticatorAlgorithmOption ResolveAlgorithm(string? algorithm)
    {
        var normalized = algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => "HMAC-SHA1"
        };
        return AlgorithmOptions.First(option => option.Value == normalized);
    }

    private int ResolvePeriod()
    {
        if (SelectedKeyType?.KeyType == AuthenticatorKeyType.CounterBased)
            return 30;
        if (!int.TryParse(PeriodSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 300)
            throw new InvalidOperationException("Period must be a whole number between 1 and 300 seconds.");
        return seconds;
    }

    private static string NormalizePeriodText(string? value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : "30";
}

public sealed record AuthenticatorKeyTypeOption(AuthenticatorKeyType KeyType, string Label);
public sealed record AuthenticatorAlgorithmOption(string Value, string Label, string ShortLabel);
public sealed record AuthenticatorDigitsOption(int Digits, string Label);

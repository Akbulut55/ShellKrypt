using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Tools;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ToolsViewModel : ViewModelBase
{
    private const int PasswordDisplayRowLength = 50;
    private const int UtilityOutputDisplayRowLength = 48;
    private const int DisplayRows = 2;
    private readonly MainWindowViewModel _root;
    private readonly ICryptoToolsService _cryptoToolsService;

    [ObservableProperty] private double passwordLength = 32;
    [ObservableProperty] private bool includeLowercase = true;
    [ObservableProperty] private bool includeUppercase = true;
    [ObservableProperty] private bool includeNumbers = true;
    [ObservableProperty] private bool includeSymbols = true;
    [ObservableProperty] private string generatedPassword = "";

    [ObservableProperty] private string hashInput = "";
    [ObservableProperty] private string hashOutput = "";

    [ObservableProperty] private string base64Input = "";
    [ObservableProperty] private string base64Output = "";

    public ToolsViewModel(MainWindowViewModel root, ICryptoToolsService cryptoToolsService)
    {
        _root = root;
        _cryptoToolsService = cryptoToolsService;
        GeneratePassword();
    }

    public string PasswordLengthDisplay => NormalizePasswordLength(PasswordLength).ToString(CultureInfo.InvariantCulture);
    public string GeneratedPasswordDisplay => FormatPasswordForDisplay(GeneratedPassword);
    public string HashOutputDisplay => FormatUtilityOutputForDisplay(HashOutput);
    public string Base64OutputDisplay => FormatUtilityOutputForDisplay(Base64Output);
    public int PasswordStrengthScore => _cryptoToolsService.AssessPasswordStrength(GeneratedPassword).Score;
    public string PasswordStrengthLabel => _cryptoToolsService.AssessPasswordStrength(GeneratedPassword).Rating switch
    {
        PasswordStrengthRating.None => "NO OPTIONS SELECTED",
        PasswordStrengthRating.Weak => "WEAK / RISKY",
        PasswordStrengthRating.Fair => "FAIR / IMPROVE",
        PasswordStrengthRating.Strong => "GOOD / STRONG",
        _ => "STRONG / SECURE"
    };
    public string PasswordStrengthBrush => _cryptoToolsService.AssessPasswordStrength(GeneratedPassword).Rating switch
    {
        PasswordStrengthRating.None => "#7b8a87",
        PasswordStrengthRating.Weak => "#ff7a7a",
        PasswordStrengthRating.Fair => "#ffb35a",
        PasswordStrengthRating.Strong => "#74f0dd",
        _ => "#4ff0df"
    };

    partial void OnPasswordLengthChanged(double value)
    {
        OnPropertyChanged(nameof(PasswordLengthDisplay));
    }

    partial void OnGeneratedPasswordChanged(string value)
    {
        OnPropertyChanged(nameof(GeneratedPasswordDisplay));
        OnPropertyChanged(nameof(PasswordStrengthScore));
        OnPropertyChanged(nameof(PasswordStrengthLabel));
        OnPropertyChanged(nameof(PasswordStrengthBrush));
    }
    partial void OnHashOutputChanged(string value) => OnPropertyChanged(nameof(HashOutputDisplay));
    partial void OnBase64OutputChanged(string value) => OnPropertyChanged(nameof(Base64OutputDisplay));
    partial void OnHashInputChanged(string value)
    {
        if (value.Length == 0)
            HashOutput = "";
    }

    partial void OnBase64InputChanged(string value)
    {
        if (value.Length == 0)
            Base64Output = "";
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        var generated = _cryptoToolsService.GeneratePassword(new PasswordGenerationOptions(
            Length: NormalizePasswordLength(PasswordLength),
            IncludeLowercase: IncludeLowercase,
            IncludeUppercase: IncludeUppercase,
            IncludeNumbers: IncludeNumbers,
            IncludeSymbols: IncludeSymbols));

        GeneratedPassword = generated ?? "";
    }

    [RelayCommand]
    private async Task CopyGeneratedPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPassword))
            return;

        await _root.CopyToClipboardAsync(GeneratedPassword);
    }

    [RelayCommand]
    private void Sha256()
    {
        HashOutput = _cryptoToolsService.ComputeSha256(HashInput);
    }

    [RelayCommand]
    private void Sha512()
    {
        HashOutput = _cryptoToolsService.ComputeSha512(HashInput);
    }

    [RelayCommand]
    private void Base64Encode()
    {
        Base64Output = _cryptoToolsService.EncodeBase64(Base64Input);
    }

    [RelayCommand]
    private void Base64Decode()
    {
        Base64Output = _cryptoToolsService.DecodeBase64(Base64Input);
    }

    private static int NormalizePasswordLength(double value)
        => Math.Clamp((int)Math.Round(value), 1, 100);

    private static string FormatPasswordForDisplay(string value)
        => FormatForDisplay(value, PasswordDisplayRowLength, DisplayRows);

    private static string FormatUtilityOutputForDisplay(string value)
        => FormatForDisplay(value, UtilityOutputDisplayRowLength, DisplayRows);

    private static string FormatForDisplay(string value, int rowLength, int rowsCount)
    {
        var rows = new string[rowsCount];
        value ??= "";

        for (var i = 0; i < rowsCount; i++)
        {
            var start = i * rowLength;
            rows[i] = start >= value.Length
                ? ""
                : value.Substring(start, Math.Min(rowLength, value.Length - start));
        }

        return string.Join(Environment.NewLine, rows);
    }

}

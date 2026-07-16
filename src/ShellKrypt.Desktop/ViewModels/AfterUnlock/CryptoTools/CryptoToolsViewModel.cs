using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Desktop.Services.Runtime;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels.AfterUnlock.CryptoTools;

public partial class CryptoToolsViewModel : ViewModelBase
{
    private const int PasswordDisplayRowLength = 50;
    private const int UtilityOutputDisplayRowLength = 48;
    private const int DisplayRows = 2;
    private readonly DesktopFeatureServices _desktop;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IPasswordStrengthService _passwordStrengthService;
    private readonly IHashService _hashService;
    private readonly IBase64Service _base64Service;

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

    public CryptoToolsViewModel(
        DesktopFeatureServices desktop,
        IPasswordGenerator passwordGenerator,
        IPasswordStrengthService passwordStrengthService,
        IHashService hashService,
        IBase64Service base64Service)
    {
        _desktop = desktop;
        _passwordGenerator = passwordGenerator;
        _passwordStrengthService = passwordStrengthService;
        _hashService = hashService;
        _base64Service = base64Service;
        GeneratePassword();
    }

    public string PasswordLengthDisplay => NormalizePasswordLength(PasswordLength).ToString(CultureInfo.InvariantCulture);
    public string GeneratedPasswordDisplay => FormatPasswordForDisplay(GeneratedPassword);
    public string HashOutputDisplay => FormatUtilityOutputForDisplay(HashOutput);
    public string Base64OutputDisplay => FormatUtilityOutputForDisplay(Base64Output);
    public int PasswordStrengthScore => _passwordStrengthService.AssessPasswordStrength(GeneratedPassword).Score;
    public string PasswordStrengthLabel => _passwordStrengthService.AssessPasswordStrength(GeneratedPassword).Rating switch
    {
        PasswordStrengthRating.None => T(_desktop.Localization, "CryptoTools.Strength.None"),
        PasswordStrengthRating.Weak => T(_desktop.Localization, "CryptoTools.Strength.Weak"),
        PasswordStrengthRating.Fair => T(_desktop.Localization, "CryptoTools.Strength.Fair"),
        PasswordStrengthRating.Strong => T(_desktop.Localization, "CryptoTools.Strength.Strong"),
        _ => throw new ArgumentOutOfRangeException()
    };
    public string PasswordStrengthBrush => _passwordStrengthService.AssessPasswordStrength(GeneratedPassword).Rating switch
    {
        PasswordStrengthRating.None => "StrengthNoneBrush",
        PasswordStrengthRating.Weak => "StrengthWeakBrush",
        PasswordStrengthRating.Fair => "StrengthFairBrush",
        PasswordStrengthRating.Strong => "StrengthStrongBrush",
        _ => throw new ArgumentOutOfRangeException()
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
        var generated = _passwordGenerator.GeneratePassword(new PasswordGenerationOptions(
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

        await _desktop.Clipboard.CopyAsync(GeneratedPassword);
        _desktop.Activity.Log("crypto-tools", "Generated password copied", "Copied a generated password from Crypto Tools.", "info", affectedItem: "Crypto Tools");
    }

    [RelayCommand]
    private void Sha256()
    {
        HashOutput = _hashService.ComputeSha256(HashInput);
    }

    [RelayCommand]
    private void Sha512()
    {
        HashOutput = _hashService.ComputeSha512(HashInput);
    }

    [RelayCommand]
    private void Base64Encode()
    {
        Base64Output = _base64Service.EncodeBase64(Base64Input);
    }

    [RelayCommand]
    private void Base64Decode()
    {
        Base64Output = _base64Service.DecodeBase64(Base64Input);
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

    public override void RefreshLocalization()
    {
        NotifyLocalized(nameof(PasswordStrengthLabel));
    }

}

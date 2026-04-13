using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ToolsViewModel : ViewModelBase
{
    private const int PasswordDisplayRowLength = 50;
    private const int UtilityOutputDisplayRowLength = 48;
    private const int DisplayRows = 2;
    private readonly MainWindowViewModel _root;

    private static readonly char[] Lowercase = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
    private static readonly char[] Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Numbers = "0123456789".ToCharArray();
    private static readonly char[] Symbols = "!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

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

    public ToolsViewModel(MainWindowViewModel root)
    {
        _root = root;
        GeneratePassword();
    }

    public string PasswordLengthDisplay => NormalizePasswordLength(PasswordLength).ToString(CultureInfo.InvariantCulture);
    public string GeneratedPasswordDisplay => FormatPasswordForDisplay(GeneratedPassword);
    public string HashOutputDisplay => FormatUtilityOutputForDisplay(HashOutput);
    public string Base64OutputDisplay => FormatUtilityOutputForDisplay(Base64Output);

    partial void OnPasswordLengthChanged(double value)
    {
        OnPropertyChanged(nameof(PasswordLengthDisplay));
    }

    partial void OnGeneratedPasswordChanged(string value) => OnPropertyChanged(nameof(GeneratedPasswordDisplay));
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
        var length = NormalizePasswordLength(PasswordLength);

        var pools = new List<char[]>();
        if (IncludeLowercase) pools.Add(Lowercase);
        if (IncludeUppercase) pools.Add(Uppercase);
        if (IncludeNumbers) pools.Add(Numbers);
        if (IncludeSymbols) pools.Add(Symbols);

        if (pools.Count == 0)
        {
            return;
        }

        var chars = new List<char>(length);

        if (length >= pools.Count)
        {
            foreach (var pool in pools)
                chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        var all = pools.SelectMany(p => p).ToArray();
        while (chars.Count < length)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        Shuffle(chars);
        GeneratedPassword = new string(chars.ToArray());
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
        if (HashInput.Length == 0)
        {
            HashOutput = "";
            return;
        }

        HashOutput = ComputeHash(HashInput, SHA256.HashData);
    }

    [RelayCommand]
    private void Sha512()
    {
        if (HashInput.Length == 0)
        {
            HashOutput = "";
            return;
        }

        HashOutput = ComputeHash(HashInput, SHA512.HashData);
    }

    [RelayCommand]
    private void Base64Encode()
    {
        if (Base64Input.Length == 0)
        {
            Base64Output = "";
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(Base64Input ?? "");
        Base64Output = Convert.ToBase64String(bytes);
    }

    [RelayCommand]
    private void Base64Decode()
    {
        if (Base64Input.Trim().Length == 0)
        {
            Base64Output = "";
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String((Base64Input ?? "").Trim());
            Base64Output = Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            Base64Output = "";
        }
    }

    private static string ComputeHash(string input, Func<byte[], byte[]> hash)
    {
        var bytes = Encoding.UTF8.GetBytes(input ?? "");
        return Convert.ToHexString(hash(bytes)).ToLowerInvariant();
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

    private static void Shuffle(IList<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}

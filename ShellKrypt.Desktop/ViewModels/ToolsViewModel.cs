using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ToolsViewModel : ViewModelBase
{
    private static readonly char[] Lowercase = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
    private static readonly char[] Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Numbers = "0123456789".ToCharArray();
    private static readonly char[] Symbols = "!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

    [ObservableProperty] private string passwordLengthText = "20";
    [ObservableProperty] private bool includeLowercase = true;
    [ObservableProperty] private bool includeUppercase = true;
    [ObservableProperty] private bool includeNumbers = true;
    [ObservableProperty] private bool includeSymbols = true;
    [ObservableProperty] private string generatedPassword = "";

    [ObservableProperty] private string hashInput = "";
    [ObservableProperty] private string hashOutput = "";

    [ObservableProperty] private string base64Input = "";
    [ObservableProperty] private string base64Output = "";

    [ObservableProperty] private string statusMessage = "Ready.";

    [RelayCommand]
    private void GeneratePassword()
    {
        if (!int.TryParse(PasswordLengthText, out var length) || length < 8 || length > 128)
        {
            StatusMessage = "Password length must be between 8 and 128.";
            return;
        }

        var pools = new List<char[]>();
        if (IncludeLowercase) pools.Add(Lowercase);
        if (IncludeUppercase) pools.Add(Uppercase);
        if (IncludeNumbers) pools.Add(Numbers);
        if (IncludeSymbols) pools.Add(Symbols);

        if (pools.Count == 0)
        {
            StatusMessage = "Select at least one character set.";
            return;
        }

        var chars = new List<char>(length);

        foreach (var pool in pools)
            chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);

        var all = pools.SelectMany(p => p).ToArray();
        while (chars.Count < length)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        Shuffle(chars);
        GeneratedPassword = new string(chars.ToArray());
        StatusMessage = $"Generated a {length}-character password.";
    }

    [RelayCommand]
    private void Sha256()
    {
        HashOutput = ComputeHash(HashInput, SHA256.HashData);
        StatusMessage = "SHA-256 updated.";
    }

    [RelayCommand]
    private void Sha512()
    {
        HashOutput = ComputeHash(HashInput, SHA512.HashData);
        StatusMessage = "SHA-512 updated.";
    }

    [RelayCommand]
    private void Base64Encode()
    {
        var bytes = Encoding.UTF8.GetBytes(Base64Input ?? "");
        Base64Output = Convert.ToBase64String(bytes);
        StatusMessage = "Base64 encoded.";
    }

    [RelayCommand]
    private void Base64Decode()
    {
        try
        {
            var bytes = Convert.FromBase64String((Base64Input ?? "").Trim());
            Base64Output = Encoding.UTF8.GetString(bytes);
            StatusMessage = "Base64 decoded.";
        }
        catch (FormatException)
        {
            Base64Output = "";
            StatusMessage = "Input is not valid Base64.";
        }
    }

    private static string ComputeHash(string input, Func<byte[], byte[]> hash)
    {
        var bytes = Encoding.UTF8.GetBytes(input ?? "");
        return Convert.ToHexString(hash(bytes)).ToLowerInvariant();
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

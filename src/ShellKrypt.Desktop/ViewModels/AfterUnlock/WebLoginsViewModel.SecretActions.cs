using CommunityToolkit.Mvvm.Input;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel
{
    private static readonly char[] LoginPasswordChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

    [RelayCommand]
    private void TogglePassword(WebLoginRowVm row)
    {
        row.IsPasswordVisible = !row.IsPasswordVisible;
    }

    [RelayCommand]
    private async Task CopyPasswordAsync(WebLoginRowVm row)
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(row.Password))
        {
            Error = T(_root, "WebLogins.Error.NoPassword");
            return;
        }

        await _root.CopyToClipboardAsync(row.Password);
        _root.LogActivity("web", "Web login password copied", $"Copied password for {row.Title}.", "info", affectedItem: row.Title);
    }

    [RelayCommand]
    private void ToggleAddPasswordVisibility()
    {
        IsAddPasswordVisible = !IsAddPasswordVisible;
    }

    [RelayCommand]
    private void GenerateAddPassword()
    {
        AddPassword = GenerateStrongPassword();
        IsAddPasswordVisible = true;
        Error = "";
    }

    [RelayCommand]
    private async Task CopyAddPasswordAsync()
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(AddPassword))
        {
            Error = T(_root, "WebLogins.Error.NoGeneratedPassword");
            return;
        }

        await _root.CopyToClipboardAsync(AddPassword);
    }

    private static string GenerateStrongPassword(int length = GeneratedLoginPasswordLength)
    {
        var chars = new char[length];

        for (var i = 0; i < chars.Length; i++)
            chars[i] = LoginPasswordChars[RandomNumberGenerator.GetInt32(LoginPasswordChars.Length)];

        return new string(chars);
    }
}

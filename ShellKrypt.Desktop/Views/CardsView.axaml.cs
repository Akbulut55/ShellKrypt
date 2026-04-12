using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ShellKrypt.Desktop.ViewModels;
using System;

namespace ShellKrypt.Desktop.Views;

public partial class CardsView : UserControl
{
    private bool _formattingAddCardNumber;

    public CardsView()
    {
        InitializeComponent();
    }

    private void AddCardNumberTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_formattingAddCardNumber || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? "";
        var formatted = CardRowVm.FormatCardNumber(
            text,
            maxDigits: CardRowVm.StandardCardNumberMaxDigits,
            includeTrailingSeparator: true);
        if (text == formatted)
            return;

        var digitsBeforeCaret = CountDigitsBeforeCaret(text, textBox.CaretIndex);
        _formattingAddCardNumber = true;
        textBox.Text = formatted;
        textBox.CaretIndex = GetCaretIndexAfterDigits(formatted, digitsBeforeCaret);
        _formattingAddCardNumber = false;
    }

    private static int CountDigitsBeforeCaret(string text, int caretIndex)
    {
        var safeCaretIndex = Math.Clamp(caretIndex, 0, text.Length);
        var count = 0;
        for (var i = 0; i < safeCaretIndex; i++)
        {
            if (char.IsDigit(text[i]))
                count++;
        }

        return count;
    }

    private static int GetCaretIndexAfterDigits(string text, int digitCount)
    {
        if (digitCount <= 0)
            return 0;

        var seenDigits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
                continue;

            seenDigits++;
            if (seenDigits == digitCount)
            {
                var index = i + 1;
                if (index < text.Length && text[index] == ' ')
                    index++;

                return index;
            }
        }

        return text.Length;
    }
}

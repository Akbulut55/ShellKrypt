using System;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;

public partial class CardEditorView : UserControl
{
    private bool _formattingCardNumber;

    public CardEditorView() => InitializeComponent();

    private void CardNumberTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_formattingCardNumber || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? "";
        var formatted = CardRowVm.FormatCardNumber(
            text,
            CardRowVm.StandardCardNumberMaxDigits,
            includeTrailingSeparator: true);
        if (text == formatted)
            return;

        var digitsBeforeCaret = CountDigitsBeforeCaret(text, textBox.CaretIndex);
        _formattingCardNumber = true;
        textBox.Text = formatted;
        textBox.CaretIndex = GetCaretIndexAfterDigits(formatted, digitsBeforeCaret);
        _formattingCardNumber = false;
    }

    private static int CountDigitsBeforeCaret(string text, int caretIndex)
    {
        var count = 0;
        for (var index = 0; index < Math.Clamp(caretIndex, 0, text.Length); index++)
            if (char.IsDigit(text[index]))
                count++;

        return count;
    }

    private static int GetCaretIndexAfterDigits(string text, int digitCount)
    {
        if (digitCount <= 0)
            return 0;

        var seenDigits = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsDigit(text[index]) || ++seenDigits != digitCount)
                continue;

            var caretIndex = index + 1;
            return caretIndex < text.Length && text[caretIndex] == ' ' ? caretIndex + 1 : caretIndex;
        }

        return text.Length;
    }
}

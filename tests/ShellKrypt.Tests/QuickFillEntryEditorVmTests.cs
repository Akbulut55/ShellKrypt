using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.ViewModels.QuickFill;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class QuickFillEntryEditorVmTests
{
    [Fact]
    public void ManualField_AddsLabelValueAndTextKind()
    {
        var editor = CreateEditor();
        editor.OwnedFieldLabel = "Tenant";
        editor.OwnedFieldValue = "corp";

        editor.AddOwnedFieldCommand.Execute(null);

        var field = Assert.Single(editor.BuildInput().Fields);
        Assert.Equal("Tenant", field.Label);
        Assert.Equal("corp", field.Value);
        Assert.Equal(QuickFillFieldKind.Text, field.Kind);
        Assert.Equal(QuickFillFieldSourceKind.Owned, field.SourceKind);
    }

    [Fact]
    public void CreditCardLinkedField_StoresEmptyValueAndLinkOnly()
    {
        var editor = CreateEditor();
        var card = new CardEntry(
            "card-1",
            "Travel card",
            "Bank",
            "User",
            "4111111111111111",
            1,
            2030,
            "123",
            "",
            "",
            "Visa",
            "",
            "");

        editor.SetLinkedSources([], [card], [], []);
        editor.SelectedCreditCardOption = editor.CreditCardOptions.Single(option => option.Id == card.Id);
        editor.SelectedCreditCardFieldOption = editor.CreditCardFieldOptions.Single(option => option.FieldName == "number");

        editor.AddCreditCardFieldCommand.Execute(null);

        var field = Assert.Single(editor.BuildInput().Fields);
        Assert.Equal(QuickFillFieldSourceKind.CreditCard, field.SourceKind);
        Assert.Equal(card.Id, field.LinkedItemId);
        Assert.Equal("number", field.LinkedFieldName);
        Assert.Equal("", field.Value);
    }

    [Fact]
    public void KeyCapture_PreviewsBeforeConfirmAndCancelLeavesSequenceUnchanged()
    {
        var editor = CreateEditor();
        editor.OpenAddStepModalCommand.Execute(null);
        editor.SelectAddKeyModeCommand.Execute(null);

        editor.AddCapturedKeyStep(QuickFillKeystrokeKind.A, QuickFillKeyModifiers.Ctrl);

        Assert.True(editor.HasPendingKeyStep);
        Assert.Contains("Ctrl+A", editor.PendingKeyPreviewText, StringComparison.Ordinal);
        Assert.Empty(editor.SequenceSteps);

        editor.ClearPendingKeyStepCommand.Execute(null);

        Assert.False(editor.HasPendingKeyStep);
        Assert.Empty(editor.SequenceSteps);
    }

    [Fact]
    public void KeyCapture_ConfirmAddsSequenceStep()
    {
        var editor = CreateEditor();
        editor.OpenAddStepModalCommand.Execute(null);
        editor.SelectAddKeyModeCommand.Execute(null);

        editor.AddCapturedKeyStep(QuickFillKeystrokeKind.Tab, QuickFillKeyModifiers.Shift);
        editor.ConfirmPendingKeyStepCommand.Execute(null);

        var step = Assert.Single(editor.BuildInput().SequenceSteps!);
        Assert.Equal(QuickFillSequenceStepKind.Keystroke, step.Kind);
        Assert.Equal(QuickFillKeystrokeKind.Tab, step.Keystroke);
        Assert.Equal(QuickFillKeyModifiers.Shift, step.Modifiers);
        Assert.False(editor.HasPendingKeyStep);
    }

    [Fact]
    public void SequenceChipLabels_DoNotExposeSensitiveValues()
    {
        var editor = CreateEditor();
        editor.OwnedFieldLabel = "Password";
        editor.OwnedFieldValue = "secret-password";

        editor.AddOwnedFieldCommand.Execute(null);

        var step = Assert.Single(editor.SequenceSteps);
        Assert.Equal("Password", step.DisplayLabel);
        Assert.DoesNotContain("secret-password", step.DisplayLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", step.DisplaySubLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInput_PreservesPopulatedEntryStateAndSequenceOrder()
    {
        var entry = new QuickFillEntry(
            "entry-1",
            "Portal",
            "Work",
            false,
            new QuickFillTargetRule("chrome", "Login"),
            [
                new QuickFillField("password", "Password", QuickFillFieldKind.Password, true, 0, QuickFillFieldSourceKind.Owned, "secret", "", "", "")
            ],
            true,
            "notes",
            "",
            "",
            [
                new QuickFillSequenceStep("field", QuickFillSequenceStepKind.Field, 0, "password", QuickFillKeystrokeKind.Tab, "", 0),
                new QuickFillSequenceStep("enter", QuickFillSequenceStepKind.Keystroke, 1, "", QuickFillKeystrokeKind.Enter, "", 0)
            ]);
        var editor = CreateEditor();

        editor.Populate(entry);
        var input = editor.BuildInput();

        Assert.Equal("Portal", input.Name);
        Assert.Equal("Work", input.Category);
        Assert.False(input.Enabled);
        Assert.Equal("chrome", input.Target.ProcessName);
        Assert.Equal("Login", input.Target.WindowTitleContains);
        Assert.True(input.PressEnterAfterFill);
        Assert.Equal("notes", input.Notes);
        Assert.Equal(["field", "enter"], input.SequenceSteps!.Select(step => step.Id).ToArray());
    }

    private static QuickFillEntryEditorVm CreateEditor()
        => new((key, args) => args.Length == 0 ? Fallback(key) : string.Format(Fallback(key), args));

    private static string Fallback(string key)
        => key switch
        {
            "QuickFill.Editor.NewEntry" => "New Quick Fill entry",
            "QuickFill.Sequence.PendingKeyPreview" => "Captured: {0}",
            "QuickFill.Sequence.PendingKeyEmpty" => "No key captured yet.",
            "QuickFill.Sequence.ConfiguredCount" => "{0} steps configured",
            "QuickFill.Editor.TargetNotSet" => "No target selected",
            "QuickFill.Field.Text" => "Text",
            "QuickFill.Source.Manual" => "Manual",
            "QuickFill.Source.WebLogin" => "Web Login",
            "QuickFill.Source.CreditCard" => "Credit Card",
            "QuickFill.Source.ApiKey" => "API Key",
            "QuickFill.Source.Authenticator" => "Authenticator",
            "QuickFill.Field.Username" => "Username",
            "QuickFill.Field.Email" => "Email",
            "QuickFill.Field.Password" => "Password",
            "QuickFill.Field.Url" => "URL",
            "QuickFill.Field.Cardholder" => "Cardholder",
            "QuickFill.Field.CardNumber" => "Card number",
            "QuickFill.Field.ExpiryMonth" => "Expiry month",
            "QuickFill.Field.ExpiryYear" => "Expiry year",
            "QuickFill.Field.Expiry" => "Expiry",
            "QuickFill.Field.Cvc" => "CVC",
            "QuickFill.Field.Bank" => "Bank",
            "QuickFill.Sequence.Keystroke.Tab" => "Tab",
            "QuickFill.Sequence.Keystroke.Enter" => "Enter",
            _ => key
        };
}

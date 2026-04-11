namespace ShellKrypt.Core.Items;

public sealed record CardPayload(
    string Title,
    string Cardholder,
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvc,
    string Notes,
    string Issuer = "",
    string? Bank = null
);

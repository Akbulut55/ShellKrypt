using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Application.Authenticator;

public sealed class AuthenticatorQrImportService
{
    private readonly IAuthenticatorQrDecoder _decoder;
    private readonly IOtpAuthUriParser _parser;

    public AuthenticatorQrImportService(IAuthenticatorQrDecoder decoder, IOtpAuthUriParser parser)
    {
        _decoder = decoder;
        _parser = parser;
    }

    public ParsedOtpAuthSecret Import(Stream imageStream, string missingQrMessage)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        var value = _decoder.Decode(imageStream);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(missingQrMessage);

        return _parser.Parse(value);
    }
}

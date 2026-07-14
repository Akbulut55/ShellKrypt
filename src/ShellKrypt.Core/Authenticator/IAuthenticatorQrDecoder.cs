namespace ShellKrypt.Core.Authenticator;

public interface IAuthenticatorQrDecoder
{
    string? Decode(Stream imageStream);
}

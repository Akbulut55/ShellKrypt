namespace ShellKrypt.Core.Authenticator;

public interface IOtpAuthUriParser
{
    ParsedOtpAuthSecret Parse(string otpauthUri);
}

namespace ShellKrypt.Core.Authenticator;

public interface IOneTimePasswordGenerator
{
    AuthenticatorCodeSnapshot GetCurrentCode(AuthenticatorEntry entry, DateTimeOffset? now = null);
}

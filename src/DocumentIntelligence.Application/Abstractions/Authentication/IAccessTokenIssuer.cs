namespace DocumentIntelligence.Application.Abstractions.Authentication;

/// <summary>
/// Issues short-lived JWT access tokens.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount user);
}

using System.Security.Claims;
using System.Text;
using DocumentIntelligence.Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DocumentIntelligence.Infrastructure.Authentication;

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(UserAccount user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + jwt.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) =>
        new(Encoding.UTF8.GetBytes(signingKey));
}

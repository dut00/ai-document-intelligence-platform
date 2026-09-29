using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Domain.Users;
using DocumentIntelligence.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class JwtAccessTokenIssuerTests
{
    private static readonly JwtOptions _options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-with-at-least-32-chars",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
    };

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Issued_token_is_valid_and_carries_user_claims()
    {
        var user = new UserAccount(UserId.New(), "user@example.com");
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), _time);

        var token = issuer.Issue(user);

        token.ExpiresAt.ShouldBe(_time.GetUtcNow().AddMinutes(15));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(_options.SigningKey),
            // The token is dated by the fake clock, so lifetime is asserted via ExpiresAt instead.
            ValidateLifetime = false,
        });

        validation.IsValid.ShouldBeTrue(validation.Exception?.Message);
        validation.Claims[JwtRegisteredClaimNames.Sub].ShouldBe(user.Id.ToString());
        validation.Claims[JwtRegisteredClaimNames.Email].ShouldBe(user.Email);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), _time);
        var token = issuer.Issue(new UserAccount(UserId.New(), "user@example.com"));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey("a-completely-different-signing-key-123456"),
            ValidateLifetime = false,
        });

        validation.IsValid.ShouldBeFalse();
    }
}

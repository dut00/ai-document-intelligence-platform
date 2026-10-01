using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class JwtOptionsValidationTests
{
    private const string SecretKey = "a-secret-key-of-at-least-thirty-two-characters";

    [Theory]
    [InlineData("local-development-signing-key-not-for-production-use")]
    [InlineData("local-docker-signing-key-not-for-production-use")]
    public void Published_signing_key_is_refused_outside_development(string publishedKey)
    {
        using var services = Build(Environments.Production, publishedKey);

        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<JwtOptions>>().Value)
            .Message.ShouldContain("published in the repository");
    }

    [Fact]
    public void Published_signing_key_is_accepted_in_development()
    {
        using var services = Build(Environments.Development, "local-development-signing-key-not-for-production-use");

        services.GetRequiredService<IOptions<JwtOptions>>().Value.SigningKey.ShouldNotBeEmpty();
    }

    [Fact]
    public void Secret_signing_key_is_accepted_in_production()
    {
        using var services = Build(Environments.Production, SecretKey);

        services.GetRequiredService<IOptions<JwtOptions>>().Value.SigningKey.ShouldBe(SecretKey);
    }

    private static ServiceProvider Build(string environmentName, string signingKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:SigningKey"] = signingKey,
            })
            .Build();

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(environment);
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);
        services.AddJwtAuthentication();

        return services.BuildServiceProvider();
    }
}

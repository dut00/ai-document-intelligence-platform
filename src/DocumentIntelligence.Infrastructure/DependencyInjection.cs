using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Infrastructure.Authentication;
using DocumentIntelligence.Infrastructure.Identity;
using DocumentIntelligence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocumentIntelligence.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddPersistence(configuration);
        services.AddIdentityAndTokens();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DatabaseConnectionName}' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }

    private static void AddIdentityAndTokens(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the raw JWT claim names ("sub", "email") instead of the legacy SOAP claim URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "email",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }
}

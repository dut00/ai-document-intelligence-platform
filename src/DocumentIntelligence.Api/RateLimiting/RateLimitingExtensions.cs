using System.Globalization;
using System.Threading.RateLimiting;
using DocumentIntelligence.Api.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Api.RateLimiting;

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Fixed window per client IP address: slows down credential stuffing and account creation.
    /// </summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Token bucket per user: allows a burst of uploads, then a steady rate that bounds the AI cost.
    /// </summary>
    public const string UploadPolicy = "upload";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            limiter.AddPolicy(AuthPolicy, context =>
            {
                var options = GetOptions(context);

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthPermitLimit,
                        Window = options.AuthWindow,
                        QueueLimit = 0,
                    });
            });

            limiter.AddPolicy(UploadPolicy, context =>
            {
                var options = GetOptions(context);

                // The endpoint requires authentication, and authorization runs first, so the user is known.
                return RateLimitPartition.GetTokenBucketLimiter(
                    context.User.GetUserId()?.Value.ToString() ?? "anonymous",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = options.UploadTokenLimit,
                        TokensPerPeriod = options.UploadTokensPerPeriod,
                        ReplenishmentPeriod = options.UploadReplenishmentPeriod,
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }

    private static RateLimitingOptions GetOptions(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "The rate limit was exceeded. Try again later.",
            },
        });
    }
}

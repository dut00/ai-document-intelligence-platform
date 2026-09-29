using System.Security.Claims;
using DocumentIntelligence.Domain.Users;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DocumentIntelligence.Api.Extensions;

internal static class ClaimsPrincipalExtensions
{
    public static UserId? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? new UserId(id) : null;
}

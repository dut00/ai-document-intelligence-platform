using Microsoft.AspNetCore.Identity;

namespace DocumentIntelligence.Infrastructure.Identity;

/// <summary>
/// Identity's user record. The email doubles as the user name.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;

using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Infrastructure.Authentication;
using DocumentIntelligence.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Infrastructure.Persistence;

/// <summary>
/// The single database: Identity users (no roles), refresh tokens and, later, documents.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IUnitOfWork
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

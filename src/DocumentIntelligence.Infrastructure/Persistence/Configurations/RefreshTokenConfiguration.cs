using DocumentIntelligence.Infrastructure.Authentication;
using DocumentIntelligence.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        // Hex-encoded SHA-256.
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.ReplacedByHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.Version).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class AnalysisUsageConfiguration : IEntityTypeConfiguration<AnalysisUsage>
{
    public void Configure(EntityTypeBuilder<AnalysisUsage> builder)
    {
        builder.ToTable("AnalysisUsage");
        builder.HasKey(usage => usage.Id);

        // One row per document however often its processing is retried or redelivered. No foreign key:
        // the row must outlive the document.
        builder.HasIndex(usage => usage.DocumentId).IsUnique();

        // The daily limits count a recent range, overall and per owner, for every processed document.
        builder.HasIndex(usage => usage.AnalyzedAt);
        builder.HasIndex(usage => new { usage.OwnerId, usage.AnalyzedAt });
    }
}

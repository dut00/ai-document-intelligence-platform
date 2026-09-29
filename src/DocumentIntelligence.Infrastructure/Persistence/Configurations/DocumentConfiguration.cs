using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(document => document.Id);
        builder.Ignore(document => document.DomainEvents);

        // Value objects are stored as plain columns through converters.
        builder.Property(document => document.Id)
            .HasConversion(id => id.Value, value => new DocumentId(value))
            .ValueGeneratedNever();

        builder.Property(document => document.OwnerId)
            .HasConversion(id => id.Value, value => new UserId(value));

        builder.Property(document => document.FileName)
            .HasConversion(name => name.Value, value => new FileName(value))
            .HasMaxLength(FileName.MaxLength);

        builder.Property(document => document.ContentType)
            .HasConversion(contentType => contentType.MimeType, value => ContentType.FromMimeType(value))
            .HasMaxLength(100);

        builder.Property(document => document.Size)
            .HasColumnName("SizeBytes")
            .HasConversion(size => size.Bytes, value => new FileSize(value));

        builder.Property(document => document.StorageKey)
            .HasConversion(key => key.Value, value => new StorageKey(value))
            .HasMaxLength(200);

        builder.Property(document => document.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(document => document.FailureReason).HasMaxLength(Document.MaxFailureReasonLength);
        builder.Property(document => document.UploadedAt);
        builder.Property(document => document.UpdatedAt);
        builder.Property(document => document.ProcessedAt);

        // PostgreSQL's xmin: concurrent status transitions of the same document conflict instead of overwriting each other.
        builder.Property<uint>("Version").IsRowVersion();

        // Serves the owner's list, newest first.
        builder.HasIndex(document => new { document.OwnerId, document.UploadedAt });

        builder.OwnsOne(document => document.Analysis, ConfigureAnalysis);
    }

    private static void ConfigureAnalysis(OwnedNavigationBuilder<Document, DocumentAnalysis> analysis)
    {
        analysis.ToTable("DocumentAnalyses");
        analysis.WithOwner().HasForeignKey(a => a.Id);
        analysis.HasKey(a => a.Id);

        analysis.Property(a => a.Id)
            .HasColumnName("DocumentId")
            .HasConversion(id => id.Value, value => new DocumentId(value));

        analysis.Property(a => a.DocumentType).HasMaxLength(200);
        analysis.Property(a => a.Summary);
        analysis.Property(a => a.Model).HasMaxLength(100);
        analysis.Property(a => a.CreatedAt);

        analysis.Property(a => a.Entities).HasJsonConversion();
        analysis.Property(a => a.ImportantDates).HasJsonConversion();
        analysis.Property(a => a.FinancialInformation).HasJsonConversion();
        analysis.Property(a => a.PotentialRisks).HasJsonConversion();
    }
}

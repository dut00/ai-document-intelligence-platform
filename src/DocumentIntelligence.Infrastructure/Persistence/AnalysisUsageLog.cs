using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocumentIntelligence.Infrastructure.Persistence;

internal sealed class AnalysisUsageLog(ApplicationDbContext dbContext) : IAnalysisUsageLog
{
    public Task<bool> IsRecordedAsync(DocumentId documentId, CancellationToken cancellationToken) =>
        dbContext.AnalysisUsage.AnyAsync(usage => usage.DocumentId == documentId.Value, cancellationToken);

    public async Task RecordAsync(UserId ownerId, DocumentId documentId, DateTimeOffset analyzedAt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenOwnConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "AnalysisUsage" ("Id", "OwnerId", "DocumentId", "AnalyzedAt")
            VALUES (@id, @ownerId, @documentId, @analyzedAt)
            ON CONFLICT ("DocumentId") DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("ownerId", ownerId.Value);
        command.Parameters.AddWithValue("documentId", documentId.Value);
        command.Parameters.AddWithValue("analyzedAt", analyzedAt.ToUniversalTime());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> RecordRetryAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenOwnConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            UPDATE "AnalysisUsage" SET "Attempts" = "Attempts" + 1
            WHERE "DocumentId" = @documentId
            RETURNING "Attempts"
            """,
            connection);
        command.Parameters.AddWithValue("documentId", documentId.Value);

        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Document {documentId} has no usage record."));
    }

    public async Task RefundAttemptAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenOwnConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            UPDATE "AnalysisUsage" SET "Attempts" = GREATEST("Attempts" - 1, 0)
            WHERE "DocumentId" = @documentId
            """,
            connection);
        command.Parameters.AddWithValue("documentId", documentId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<int> CountSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        dbContext.AnalysisUsage.CountAsync(usage => usage.AnalyzedAt >= since, cancellationToken);

    public Task<int> CountForOwnerSinceAsync(UserId ownerId, DateTimeOffset since, CancellationToken cancellationToken) =>
        dbContext.AnalysisUsage.CountAsync(usage => usage.OwnerId == ownerId.Value && usage.AnalyzedAt >= since, cancellationToken);

    // A connection of its own, never enlisted in the consumer's transaction: a write must be committed before
    // the AI is called and must survive a rollback of the processing attempt.
    private async Task<NpgsqlConnection> OpenOwnConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString()) { Enlist = false };
        var connection = new NpgsqlConnection(connectionString.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

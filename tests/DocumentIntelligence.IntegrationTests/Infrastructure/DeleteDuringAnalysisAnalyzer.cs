using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Decorates the configured analyzer: for a document whose text is <see cref="Marker"/> followed by its
/// file name, the document row is deleted while the analysis runs, as if its owner deleted it meanwhile.
/// </summary>
public sealed class DeleteDuringAnalysisAnalyzer(IDocumentAnalyzer inner, IServiceScopeFactory scopes) : IDocumentAnalyzer
{
    public const string Marker = "[simulate-delete-during-analysis]";

    public string Model => inner.Model;

    public async Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        if (text.StartsWith(Marker, StringComparison.Ordinal))
        {
            var fileName = text[Marker.Length..].Trim();

            // A connection of its own, like the API's delete request.
            await using var scope = scopes.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.ExecuteSqlAsync($"""DELETE FROM "Documents" WHERE "FileName" = {fileName}""", cancellationToken);
        }

        return await inner.AnalyzeAsync(text, cancellationToken);
    }
}

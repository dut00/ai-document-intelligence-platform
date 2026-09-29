namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <param name="Type">A name of <c>EntityType</c>, e.g. "Organization".</param>
public sealed record AnalyzedEntity(string Type, string Name);

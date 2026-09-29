namespace DocumentIntelligence.Application.Documents;

public sealed record DocumentStatsResponse(int Total, int Pending, int Processing, int Completed, int Failed);

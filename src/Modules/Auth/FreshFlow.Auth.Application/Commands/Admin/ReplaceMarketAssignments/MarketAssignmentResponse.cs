namespace FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

public record MarketAssignmentResponse(Guid UserId, IReadOnlyList<Guid> MarketIds);

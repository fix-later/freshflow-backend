using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

public record ReplaceMarketAssignmentsCommand(
    Guid UserId,
    IReadOnlyList<Guid> MarketIds,
    Guid? AssignedById)
    : IRequest<Result<MarketAssignmentResponse>>;

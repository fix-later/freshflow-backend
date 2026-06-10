using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetMarketAssignments;

public record GetMarketAssignmentsQuery(Guid UserId)
    : IRequest<Result<MarketAssignmentResponse>>;

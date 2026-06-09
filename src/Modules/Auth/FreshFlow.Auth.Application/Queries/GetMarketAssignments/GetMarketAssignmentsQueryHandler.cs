using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetMarketAssignments;

internal sealed class GetMarketAssignmentsQueryHandler(
    IUserRepository users,
    IUserMarketAssignmentRepository assignments)
    : IRequestHandler<GetMarketAssignmentsQuery, Result<MarketAssignmentResponse>>
{
    public async Task<Result<MarketAssignmentResponse>> Handle(
        GetMarketAssignmentsQuery request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<MarketAssignmentResponse>.Failure(
                Error.NotFound("User", request.UserId));

        var rows = await assignments.GetByUserIdAsync(request.UserId, ct);
        var marketIds = rows.Select(a => a.MarketId).ToList().AsReadOnly();

        return Result<MarketAssignmentResponse>.Success(
            new MarketAssignmentResponse(request.UserId, marketIds));
    }
}

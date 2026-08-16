using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

internal sealed class ReplaceMarketAssignmentsCommandHandler(
    IUserRepository users,
    IMarketValidator marketValidator,
    IUserMarketAssignmentRepository assignments)
    : IRequestHandler<ReplaceMarketAssignmentsCommand, Result<MarketAssignmentResponse>>
{
    public async Task<Result<MarketAssignmentResponse>> Handle(
        ReplaceMarketAssignmentsCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<MarketAssignmentResponse>.Failure(
                Error.NotFound("User", request.UserId));

        if (user.Role.Name != RoleNames.MarketAgent)
            return Result<MarketAssignmentResponse>.Failure(
                Error.Validation("INVALID_ASSIGNMENT_TARGET",
                    $"User '{request.UserId}' must have the '{RoleNames.MarketAgent}' role to be assigned to a market."));

        // Validate each market is active before touching any data
        foreach (var marketId in request.MarketIds)
        {
            if (!await marketValidator.IsActiveMarketAsync(marketId, ct))
                return Result<MarketAssignmentResponse>.Failure(
                    Error.Validation("INVALID_MARKET",
                        $"Market '{marketId}' does not exist or is inactive."));
        }

        // Remove all existing assignments for this user
        var existing = await assignments.GetByUserIdAsync(request.UserId, ct);
        if (existing.Count > 0)
            await assignments.RemoveRangeAsync(existing, ct);

        // Add the new set
        foreach (var marketId in request.MarketIds)
            await assignments.AddAsync(
                new UserMarketAssignment(request.UserId, marketId, request.AssignedById), ct);

        await assignments.SaveChangesAsync(ct);

        return Result<MarketAssignmentResponse>.Success(
            new MarketAssignmentResponse(request.UserId, request.MarketIds));
    }
}

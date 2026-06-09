using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantApprovalStatus;

internal sealed class GetRestaurantApprovalStatusQueryHandler(IRestaurantRepository restaurants)
    : IRequestHandler<GetRestaurantApprovalStatusQuery, Result<GetRestaurantApprovalStatusResponse>>
{
    public async Task<Result<GetRestaurantApprovalStatusResponse>> Handle(
        GetRestaurantApprovalStatusQuery request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);

        if (restaurant is null)
            return Result<GetRestaurantApprovalStatusResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        return Result<GetRestaurantApprovalStatusResponse>.Success(
            new GetRestaurantApprovalStatusResponse(
                restaurant.Id,
                restaurant.Status.ToString().ToLowerInvariant(),
                restaurant.UpdatedAt));
    }
}

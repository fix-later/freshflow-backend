using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.ReactivateRestaurant;

internal sealed class ReactivateRestaurantCommandHandler(IRestaurantRepository restaurants)
    : IRequestHandler<ReactivateRestaurantCommand, Result<ReactivateRestaurantResponse>>
{
    public async Task<Result<ReactivateRestaurantResponse>> Handle(
        ReactivateRestaurantCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByIdAsync(request.RestaurantId, ct);
        if (restaurant is null)
            return Result<ReactivateRestaurantResponse>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.Status != RestaurantStatus.Suspended)
            return Result<ReactivateRestaurantResponse>.Failure(
                Error.Validation("NOT_SUSPENDED", "Only suspended restaurants can be reactivated."));

        // ApproveAsync sets Status = Active — the exact effect of reactivation for a suspended account.
        await restaurants.ApproveAsync(request.RestaurantId, ct);

        return Result<ReactivateRestaurantResponse>.Success(
            new ReactivateRestaurantResponse(restaurant.Id, restaurant.Name, true, DateTime.UtcNow));
    }
}

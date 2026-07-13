using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.SuspendRestaurant;

internal sealed class SuspendRestaurantCommandHandler(IRestaurantRepository restaurants)
    : IRequestHandler<SuspendRestaurantCommand, Result<SuspendRestaurantResponse>>
{
    public async Task<Result<SuspendRestaurantResponse>> Handle(
        SuspendRestaurantCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByIdAsync(request.RestaurantId, ct);
        if (restaurant is null)
            return Result<SuspendRestaurantResponse>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.Status == RestaurantStatus.Suspended)
            return Result<SuspendRestaurantResponse>.Failure(
                Error.Validation("ALREADY_SUSPENDED", "This restaurant is already suspended."));

        if (restaurant.Status != RestaurantStatus.Active)
            return Result<SuspendRestaurantResponse>.Failure(
                Error.Validation("NOT_ACTIVE", "Only active restaurants can be suspended."));

        await restaurants.SuspendAsync(request.RestaurantId, ct);

        return Result<SuspendRestaurantResponse>.Success(
            new SuspendRestaurantResponse(restaurant.Id, restaurant.Name, true, DateTime.UtcNow));
    }
}

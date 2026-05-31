using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;

internal sealed class ApproveRestaurantCommandHandler(IRestaurantRepository restaurants)
    : IRequestHandler<ApproveRestaurantCommand, Result<ApproveRestaurantResponse>>
{
    public async Task<Result<ApproveRestaurantResponse>> Handle(
        ApproveRestaurantCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByIdAsync(request.RestaurantId, ct);
        if (restaurant is null)
            return Result<ApproveRestaurantResponse>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.IsApproved)
            return Result<ApproveRestaurantResponse>.Failure(
                Error.Validation("ALREADY_APPROVED", "This restaurant is already approved."));

        await restaurants.ApproveAsync(request.RestaurantId, ct);

        return Result<ApproveRestaurantResponse>.Success(
            new ApproveRestaurantResponse(restaurant.Id, restaurant.Name, true, DateTime.UtcNow));
    }
}

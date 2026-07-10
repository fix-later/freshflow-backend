using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;

internal sealed class UpdateRestaurantProfileCommandHandler(IRestaurantRepository restaurants)
    : IRequestHandler<UpdateRestaurantProfileCommand, Result<UpdateRestaurantProfileResponse>>
{
    public async Task<Result<UpdateRestaurantProfileResponse>> Handle(
        UpdateRestaurantProfileCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<UpdateRestaurantProfileResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        var updated = await restaurants.UpdateProfileAsync(
            restaurant.Id,
            request.Name,
            request.Address,
            request.ContactPerson,
            request.PickupStart,
            request.PickupEnd,
            request.BusinessLicenseUrl,
            ct);

        if (updated is null)
            return Result<UpdateRestaurantProfileResponse>.Failure(
                Error.NotFound("Restaurant", restaurant.Id));

        return Result<UpdateRestaurantProfileResponse>.Success(
            new UpdateRestaurantProfileResponse(
                updated.Id,
                updated.Name,
                updated.Address,
                updated.ContactPerson,
                updated.PickupStart,
                updated.PickupEnd,
                updated.UpdatedAt,
                updated.BusinessLicenseUrl));
    }
}

using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfile;

internal sealed class GetRestaurantProfileQueryHandler(IRestaurantRepository restaurants)
    : IRequestHandler<GetRestaurantProfileQuery, Result<GetRestaurantProfileResponse>>
{
    public async Task<Result<GetRestaurantProfileResponse>> Handle(
        GetRestaurantProfileQuery request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);

        if (restaurant is null)
            return Result<GetRestaurantProfileResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        return Result<GetRestaurantProfileResponse>.Success(
            new GetRestaurantProfileResponse(
                restaurant.Id,
                restaurant.Name,
                restaurant.Status.ToString().ToLowerInvariant(),
                restaurant.Address,
                restaurant.ContactPerson,
                restaurant.PickupStart,
                restaurant.PickupEnd,
                restaurant.UpdatedAt,
                restaurant.BusinessLicenseUrl,
                restaurant.TaxCode,
                restaurant.InvoiceLegalName,
                restaurant.InvoiceAddress,
                restaurant.InvoiceEmail));
    }
}

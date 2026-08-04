using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetRestaurantProfile;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfileById;

internal sealed class GetRestaurantProfileByIdQueryHandler(IRestaurantRepository restaurants)
    : IRequestHandler<GetRestaurantProfileByIdQuery, Result<GetRestaurantProfileResponse>>
{
    public async Task<Result<GetRestaurantProfileResponse>> Handle(
        GetRestaurantProfileByIdQuery request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByIdAsync(request.RestaurantId, ct);

        if (restaurant is null)
            return Result<GetRestaurantProfileResponse>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

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

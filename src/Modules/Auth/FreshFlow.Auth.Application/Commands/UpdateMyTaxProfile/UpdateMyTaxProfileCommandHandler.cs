using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;

internal sealed class UpdateMyTaxProfileCommandHandler(IRestaurantRepository restaurants)
    : IRequestHandler<UpdateMyTaxProfileCommand, Result<UpdateMyTaxProfileResponse>>
{
    public async Task<Result<UpdateMyTaxProfileResponse>> Handle(
        UpdateMyTaxProfileCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<UpdateMyTaxProfileResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        var updated = await restaurants.UpdateTaxProfileAsync(
            restaurant.Id,
            request.TaxCode,
            request.LegalName,
            request.Address,
            request.Email,
            ct);

        if (updated is null)
            return Result<UpdateMyTaxProfileResponse>.Failure(
                Error.NotFound("Restaurant", restaurant.Id));

        return Result<UpdateMyTaxProfileResponse>.Success(
            new UpdateMyTaxProfileResponse(
                updated.Id,
                updated.TaxCode,
                updated.InvoiceLegalName,
                updated.InvoiceAddress,
                updated.InvoiceEmail,
                updated.UpdatedAt));
    }
}

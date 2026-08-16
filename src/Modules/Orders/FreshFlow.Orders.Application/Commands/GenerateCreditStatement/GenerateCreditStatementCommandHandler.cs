using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.GenerateCreditStatement;

internal sealed class GenerateCreditStatementCommandHandler(
    ICreditStatementGenerationService generationService,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GenerateCreditStatementCommand, Result<CreditStatementDto>>
{
    public async Task<Result<CreditStatementDto>> Handle(
        GenerateCreditStatementCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result<CreditStatementDto>.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (!request.IsAdmin)
        {
            var ownedRestaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (ownedRestaurant is null || ownedRestaurant.RestaurantId != request.RestaurantId)
                return Result<CreditStatementDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant credit account is not accessible."));
        }

        return await generationService.GenerateAsync(request.RestaurantId, request.Year, request.Month, cancellationToken);
    }
}

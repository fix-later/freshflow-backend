using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

internal sealed class GetCreditTransactionsQueryHandler(
    ICreditRepository creditRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetCreditTransactionsQuery, Result<IReadOnlyList<CreditTransactionDto>>>
{
    public async Task<Result<IReadOnlyList<CreditTransactionDto>>> Handle(
        GetCreditTransactionsQuery request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result<IReadOnlyList<CreditTransactionDto>>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (!request.IsAdmin)
        {
            var ownedRestaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (ownedRestaurant is null || ownedRestaurant.RestaurantId != request.RestaurantId)
                return Result<IReadOnlyList<CreditTransactionDto>>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant credit account is not accessible."));
        }

        var transactions = await creditRepository.GetTransactionsAsync(request.RestaurantId, cancellationToken);
        return Result<IReadOnlyList<CreditTransactionDto>>.Success(
            transactions.Select(CreditDtoMapper.ToDto).ToList());
    }
}

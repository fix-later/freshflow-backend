using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

internal sealed class GetCreditTransactionsQueryHandler(
    ICreditRepository creditRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetCreditTransactionsQuery, Result<CreditTransactionPageDto>>
{
    public async Task<Result<CreditTransactionPageDto>> Handle(
        GetCreditTransactionsQuery request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result<CreditTransactionPageDto>.Failure(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (!request.IsAdmin)
        {
            var ownedRestaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (ownedRestaurant is null || ownedRestaurant.RestaurantId != request.RestaurantId)
                return Result<CreditTransactionPageDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant credit account is not accessible."));
        }

        var (transactions, nextCursor) = await creditRepository.GetTransactionsPageAsync(
            request.RestaurantId, request.Cursor, request.PageSize, request.From, request.To, cancellationToken);

        var items = transactions.Select(CreditDtoMapper.ToDto).ToList().AsReadOnly();
        return Result<CreditTransactionPageDto>.Success(
            new CreditTransactionPageDto(items, request.PageSize, nextCursor));
    }
}

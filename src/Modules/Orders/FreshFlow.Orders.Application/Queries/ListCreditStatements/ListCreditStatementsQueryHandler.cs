using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.ListCreditStatements;

internal sealed class ListCreditStatementsQueryHandler(
    ICreditStatementRepository statementRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ListCreditStatementsQuery, Result<CreditStatementPageDto>>
{
    public async Task<Result<CreditStatementPageDto>> Handle(
        ListCreditStatementsQuery request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result<CreditStatementPageDto>.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (!request.IsAdmin)
        {
            var ownedRestaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (ownedRestaurant is null || ownedRestaurant.RestaurantId != request.RestaurantId)
                return Result<CreditStatementPageDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant credit account is not accessible."));
        }

        var (statements, nextCursor) = await statementRepository.GetPageAsync(
            request.RestaurantId, request.Cursor, request.PageSize, cancellationToken);

        var items = statements.Select(CreditStatementDtoMapper.ToSummaryDto).ToList().AsReadOnly();
        return Result<CreditStatementPageDto>.Success(
            new CreditStatementPageDto(items, request.PageSize, nextCursor));
    }
}

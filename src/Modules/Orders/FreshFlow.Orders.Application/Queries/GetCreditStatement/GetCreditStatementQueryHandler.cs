using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetCreditStatement;

internal sealed class GetCreditStatementQueryHandler(
    ICreditStatementRepository statementRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetCreditStatementQuery, Result<CreditStatementDto>>
{
    public async Task<Result<CreditStatementDto>> Handle(
        GetCreditStatementQuery request, CancellationToken cancellationToken)
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

        var statement = await ResolveStatementAsync(request, cancellationToken);

        // Owning-restaurant scoping doubles as an IDOR guard for the by-id lookup — a
        // statement id that belongs to a different restaurant must 404, not leak data.
        if (statement is null || statement.RestaurantId != request.RestaurantId)
            return Result<CreditStatementDto>.Failure(
                Error.NotFound("CreditStatement", (object?)request.StatementId ?? $"{request.Year}-{request.Month}"));

        return Result<CreditStatementDto>.Success(CreditStatementDtoMapper.ToDto(statement));
    }

    private Task<CreditStatement?> ResolveStatementAsync(GetCreditStatementQuery request, CancellationToken ct)
    {
        if (request.StatementId.HasValue)
            return statementRepository.FindByIdAsync(request.StatementId.Value, ct);

        var (periodStart, _) = CreditStatementPeriodCalculator.ResolvePeriod(request.Year!.Value, request.Month!.Value);
        return statementRepository.FindByPeriodAsync(request.RestaurantId, periodStart, ct);
    }
}

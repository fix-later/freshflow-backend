using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.ListOrders;

internal sealed class ListOrdersQueryHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader) : IRequestHandler<ListOrdersQuery, Result<OrderListResponseDto>>
{
    public async Task<Result<OrderListResponseDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var ownershipResult = await ResolveRestaurantFilterAsync(request, cancellationToken);
        if (ownershipResult.IsFailure)
            return Result<OrderListResponseDto>.Failure(ownershipResult.Error);

        if (!TryResolveOptionalStatus(request.Status, out var status, out var statusError))
            return Result<OrderListResponseDto>.Failure(statusError);

        if (!OrderQueryParsing.TryParseSort(request.Sort, out var sortAscending))
            return Result<OrderListResponseDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "Sort must be 'createdAt:asc' or 'createdAt:desc'."));

        var createdTo = OrderQueryParsing.NormalizeCreatedTo(request.To);
        if (request.From.HasValue && createdTo.HasValue && request.From.Value > createdTo.Value)
            return Result<OrderListResponseDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "'From' must be before or equal to 'To'."));

        var criteria = new OrderSearchCriteria(
            ownershipResult.Value,
            status,
            request.From,
            createdTo,
            sortAscending,
            request.Page,
            request.PageSize);

        var (orders, total) = await orderRepository.SearchAsync(criteria, cancellationToken);
        var response = new OrderListResponseDto(
            orders.Select(OrderDtoMapper.ToListItemDto).ToList(),
            new OrderPaginationMeta(total, request.Page, request.PageSize));

        return Result<OrderListResponseDto>.Success(response);
    }

    private async Task<Result<Guid?>> ResolveRestaurantFilterAsync(
        ListOrdersQuery request, CancellationToken cancellationToken)
    {
        if (request.IsAdmin)
            return Result<Guid?>.Success(request.RestaurantId);

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<Guid?>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        if (request.RestaurantId.HasValue && request.RestaurantId.Value != restaurant.RestaurantId)
            return Result<Guid?>.Failure(
                Error.Unauthorized("FORBIDDEN", "This restaurant's orders are not accessible."));

        return Result<Guid?>.Success(restaurant.RestaurantId);
    }

    private static bool TryResolveOptionalStatus(
        string? rawStatus, out OrderStatus? status, out Error error)
    {
        status = null;
        error = Error.None;

        if (string.IsNullOrWhiteSpace(rawStatus))
            return true;

        if (!OrderQueryParsing.TryParseStatus(rawStatus, out var parsed))
        {
            error = Error.Validation("VALIDATION_ERROR", "Status is invalid.");
            return false;
        }

        status = parsed;
        return true;
    }
}

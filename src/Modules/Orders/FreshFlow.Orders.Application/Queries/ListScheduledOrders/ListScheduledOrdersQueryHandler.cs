using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrders;

internal sealed class ListScheduledOrdersQueryHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ListScheduledOrdersQuery, Result<ScheduledOrderListResponseDto>>
{
    public async Task<Result<ScheduledOrderListResponseDto>> Handle(
        ListScheduledOrdersQuery request, CancellationToken cancellationToken)
    {
        var ownershipResult = await ResolveRestaurantFilterAsync(request, cancellationToken);
        if (ownershipResult.IsFailure)
            return Result<ScheduledOrderListResponseDto>.Failure(ownershipResult.Error);

        var criteria = new ScheduledOrderSearchCriteria(
            ownershipResult.Value,
            request.IncludeCancelled,
            request.Page,
            request.PageSize);

        var (scheduledOrders, total) = await scheduledOrderRepository.SearchAsync(criteria, cancellationToken);
        var response = new ScheduledOrderListResponseDto(
            scheduledOrders.Select(ScheduledOrderDtoMapper.ToDto).ToList(),
            new OrderPaginationMeta(total, request.Page, request.PageSize));

        return Result<ScheduledOrderListResponseDto>.Success(response);
    }

    private async Task<Result<Guid?>> ResolveRestaurantFilterAsync(
        ListScheduledOrdersQuery request, CancellationToken cancellationToken)
    {
        if (request.IsAdmin)
            return Result<Guid?>.Success(request.RestaurantId);

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<Guid?>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        if (request.RestaurantId.HasValue && request.RestaurantId.Value != restaurant.RestaurantId)
            return Result<Guid?>.Failure(
                Error.Unauthorized("FORBIDDEN", "This restaurant's recurring schedules are not accessible."));

        return Result<Guid?>.Success(restaurant.RestaurantId);
    }
}

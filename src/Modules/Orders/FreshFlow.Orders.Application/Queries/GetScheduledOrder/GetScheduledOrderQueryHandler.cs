using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetScheduledOrder;

internal sealed class GetScheduledOrderQueryHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetScheduledOrderQuery, Result<ScheduledOrderDto>>
{
    public async Task<Result<ScheduledOrderDto>> Handle(
        GetScheduledOrderQuery request, CancellationToken cancellationToken)
    {
        var scheduledOrder = await scheduledOrderRepository.FindByIdAsync(request.ScheduledOrderId, cancellationToken);
        if (scheduledOrder is null)
            return Result<ScheduledOrderDto>.Failure(Error.NotFound("SCHEDULED_ORDER", request.ScheduledOrderId));

        if (!request.IsAdmin)
        {
            var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != scheduledOrder.RestaurantId)
                return Result<ScheduledOrderDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This recurring schedule is not accessible."));
        }

        return Result<ScheduledOrderDto>.Success(ScheduledOrderDtoMapper.ToDto(scheduledOrder));
    }
}

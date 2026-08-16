using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CancelScheduledOrder;

internal sealed class CancelScheduledOrderCommandHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<CancelScheduledOrderCommand, Result<ScheduledOrderDto>>
{
    public async Task<Result<ScheduledOrderDto>> Handle(
        CancelScheduledOrderCommand request, CancellationToken cancellationToken)
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

        var cancelResult = scheduledOrder.Cancel();
        if (cancelResult.IsFailure)
            return Result<ScheduledOrderDto>.Failure(cancelResult.Error);

        scheduledOrderRepository.Track(scheduledOrder);
        await scheduledOrderRepository.SaveChangesAsync(cancellationToken);

        return Result<ScheduledOrderDto>.Success(ScheduledOrderDtoMapper.ToDto(scheduledOrder));
    }
}

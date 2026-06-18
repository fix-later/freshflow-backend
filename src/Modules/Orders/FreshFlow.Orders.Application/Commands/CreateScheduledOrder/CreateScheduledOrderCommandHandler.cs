using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CreateScheduledOrder;

internal sealed class CreateScheduledOrderCommandHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<CreateScheduledOrderCommand, Result<ScheduledOrderDto>>
{
    public async Task<Result<ScheduledOrderDto>> Handle(
        CreateScheduledOrderCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<ScheduledOrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        if (!restaurant.IsApproved)
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "RESTAURANT_NOT_APPROVED", "This restaurant has not been approved to create recurring orders."));

        if (!ScheduledOrderParsing.TryParseRecurrenceType(request.RecurrenceType, out var recurrenceType))
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "RecurrenceType must be 'daily' or 'weekly'."));

        if (request.FirstRunAt <= DateTime.UtcNow)
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "SCHEDULED_ORDER_FIRST_RUN_IN_PAST", "FirstRunAt must be in the future."));

        var scheduledOrder = new ScheduledOrder(
            restaurant.RestaurantId,
            recurrenceType,
            request.FirstRunAt,
            request.Notes);

        await scheduledOrderRepository.AddAsync(scheduledOrder, cancellationToken);
        await scheduledOrderRepository.SaveChangesAsync(cancellationToken);

        return Result<ScheduledOrderDto>.Success(ScheduledOrderDtoMapper.ToDto(scheduledOrder));
    }
}

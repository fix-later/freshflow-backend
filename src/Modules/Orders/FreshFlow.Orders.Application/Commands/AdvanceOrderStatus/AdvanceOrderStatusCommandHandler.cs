using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.AdvanceOrderStatus;

internal sealed class AdvanceOrderStatusCommandHandler(
    IOrderRepository orderRepository) : IRequestHandler<AdvanceOrderStatusCommand, Result<OrderDto>>
{
    /// <summary>
    /// Pre-hub pipeline stages an ops user may advance an order to. Delivering/Delivered are
    /// excluded on purpose — advancing there without a Logistics delivery would strand the order.
    /// </summary>
    internal static readonly IReadOnlySet<OrderStatus> AllowedTargets =
        new HashSet<OrderStatus> { OrderStatus.Batched, OrderStatus.PickedUp, OrderStatus.AtHub };

    public async Task<Result<OrderDto>> Handle(AdvanceOrderStatusCommand request, CancellationToken cancellationToken)
    {
        if (!OrderQueryParsing.TryParseStatus(request.Status, out var next) || !AllowedTargets.Contains(next))
            return Result<OrderDto>.Failure(Error.Validation(
                "ORDER_STATUS_NOT_ADVANCEABLE",
                "Status must be one of: batched, picked_up, at_hub."));

        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var advanceResult = order.AdvanceStatus(next);
        if (advanceResult.IsFailure)
            return Result<OrderDto>.Failure(advanceResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}

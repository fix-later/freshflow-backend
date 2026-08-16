using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;

internal sealed class RecordOrderItemActualQuantityCommandHandler(
    IOrderRepository orderRepository) : IRequestHandler<RecordOrderItemActualQuantityCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        RecordOrderItemActualQuantityCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var adjustmentResult = order.RecordActualQuantity(request.OrderItemId, request.ActualQuantity);
        if (adjustmentResult.IsFailure)
            return Result<OrderDto>.Failure(adjustmentResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}

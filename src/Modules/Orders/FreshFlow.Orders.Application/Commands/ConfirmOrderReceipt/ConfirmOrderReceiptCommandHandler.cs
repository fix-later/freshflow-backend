using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrderReceipt;

internal sealed class ConfirmOrderReceiptCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ConfirmOrderReceiptCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        ConfirmOrderReceiptCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var receiptResult = order.ConfirmReceipt();
        if (receiptResult.IsFailure)
            return Result<OrderDto>.Failure(receiptResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}

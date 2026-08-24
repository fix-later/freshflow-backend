using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.UpdateOrderNotes;

/// <summary>
/// Error precedence:
/// 404 order missing   → ORDER_NOT_FOUND
/// 403 not owner        → FORBIDDEN
/// 409 order not draft  → ORDER_NOT_DRAFT (from domain)
/// 200 success
/// </summary>
internal sealed class UpdateOrderNotesCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<UpdateOrderNotesCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(UpdateOrderNotesCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var updateResult = order.UpdateNotes(request.Notes);
        if (updateResult.IsFailure)
            return Result<OrderDto>.Failure(updateResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}

using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.FileClaim;

internal sealed class FileClaimCommandHandler(
    IOrderRepository orders,
    IOrderClaimRepository claims,
    IRestaurantReader restaurants)
    : IRequestHandler<FileClaimCommand, Result<OrderClaimDto>>
{
    public async Task<Result<OrderClaimDto>> Handle(
        FileClaimCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<OrderClaimDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        var order = await orders.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderClaimDto>.Failure(Error.NotFound("ORDER", request.OrderId));
        if (order.RestaurantId != restaurant.RestaurantId)
            return Result<OrderClaimDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));
        if (order.Status is not (OrderStatus.AtHub or OrderStatus.Delivered))
            return Result<OrderClaimDto>.Failure(Error.Conflict(
                "CLAIM_ORDER_NOT_CLAIMABLE",
                "Claims can only be filed for orders at the hub or delivered."));
        if (request.Amount > order.TotalAmount)
            return Result<OrderClaimDto>.Failure(Error.Validation(
                "INVALID_CLAIM_AMOUNT",
                "Claim amount cannot exceed the amount charged for the order."));

        var claim = new OrderClaim(
            order.Id,
            order.RestaurantId,
            request.Amount,
            request.Reason,
            request.UserId,
            DateTime.UtcNow);

        await claims.AddAsync(claim, cancellationToken);
        await claims.SaveChangesAsync(cancellationToken);
        return Result<OrderClaimDto>.Success(OrderClaimDtoMapper.ToDto(claim));
    }
}

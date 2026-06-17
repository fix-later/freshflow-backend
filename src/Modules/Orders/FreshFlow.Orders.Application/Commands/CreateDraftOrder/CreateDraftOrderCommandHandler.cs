using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CreateDraftOrder;

/// <summary>
/// SCRUM-180 — UC-ORD-01: Create Draft Order.
///
/// Error precedence (cheapest/most authoritative first):
/// 403 restaurant missing      → JWT user has no restaurant record
/// 422 restaurant not approved → RESTAURANT_NOT_APPROVED
/// 422 invalid product         → INVALID_PRODUCT (per item)
/// 422 insufficient stock      → INSUFFICIENT_STOCK (per item)
/// 201 success
/// </summary>
internal sealed class CreateDraftOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<CreateDraftOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        CreateDraftOrderCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Resolve restaurant from JWT user (403) ──────────────────────────
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        // ── 2. Approval check (422) ─────────────────────────────────────────────
        if (!restaurant.IsApproved)
            return Result<OrderDto>.Failure(Error.Validation(
                "RESTAURANT_NOT_APPROVED", "This restaurant has not been approved to place orders."));

        // ── 3. Validate items against live market product data (422) ─────────
        var order = new Order(restaurant.RestaurantId, request.ScheduledFor, request.Notes);
        var snapshots = new Dictionary<Guid, MarketProductSnapshotDto>();

        foreach (var group in request.Items.GroupBy(i => i.MarketProductId))
        {
            var snapshot = await marketProductReader.FindAsync(group.Key, cancellationToken);
            if (snapshot is null)
                return Result<OrderDto>.Failure(Error.Validation(
                    "INVALID_PRODUCT", $"Product '{group.Key}' is not available."));

            var requestedQuantity = group.Sum(i => i.Quantity);
            if (requestedQuantity > snapshot.AvailableQuantity)
                return Result<OrderDto>.Failure(Error.Validation(
                    "INSUFFICIENT_STOCK",
                    $"Requested quantity {requestedQuantity} exceeds available stock " +
                    $"{snapshot.AvailableQuantity} for product '{group.Key}'."));

            snapshots[group.Key] = snapshot;
        }

        foreach (var requestedItem in request.Items)
        {
            var snapshot = snapshots[requestedItem.MarketProductId];
            order.AddItem(snapshot.MarketProductId, snapshot.ProductName, requestedItem.Quantity, snapshot.CurrentPrice);
        }

        // ── 4. Persist ───────────────────────────────────────────────────────
        await orderRepository.AddAsync(order, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}

using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Abstractions;

public interface ICreditService
{
    public Task<Result<CreditCheckDto>> CanChargeAsync(Guid restaurantId, decimal amount, CancellationToken ct);

    public Task<Result<RestaurantCreditDto>> ChargeAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct);

    public Task<Result<RestaurantCreditDto>> RefundAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct);

    public Task<Result<RestaurantCreditDto>> SettleAsync(
        Guid restaurantId,
        decimal amount,
        string? note,
        CancellationToken ct);
}

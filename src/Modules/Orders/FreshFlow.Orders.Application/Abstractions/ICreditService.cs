using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Enums;
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

    public Task<Result<CreditRefundDto>> RefundAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct);

    public Task<Result<RestaurantCreditDto>> SettleAsync(
        Guid restaurantId,
        Guid recordedByUserId,
        decimal amount,
        PaymentMethod paymentMethod,
        string? reference,
        string? note,
        CancellationToken ct);

    public Task<Result<RestaurantCreditDto>> SetCreditLimitAsync(
        Guid restaurantId,
        decimal newLimit,
        string? note,
        CancellationToken ct);
}

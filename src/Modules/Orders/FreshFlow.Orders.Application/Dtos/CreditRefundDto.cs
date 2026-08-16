namespace FreshFlow.Orders.Application.Dtos;

public sealed record CreditRefundDto(
    Guid TransactionId,
    RestaurantCreditDto Credit);

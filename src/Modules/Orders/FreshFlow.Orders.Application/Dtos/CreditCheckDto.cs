namespace FreshFlow.Orders.Application.Dtos;

public sealed record CreditCheckDto(
    Guid RestaurantId,
    decimal CreditLimit,
    decimal OutstandingBalance,
    decimal AvailableCredit,
    decimal RequestedAmount,
    bool CanCharge);

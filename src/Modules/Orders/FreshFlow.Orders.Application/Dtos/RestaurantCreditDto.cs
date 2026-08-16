namespace FreshFlow.Orders.Application.Dtos;

public sealed record RestaurantCreditDto(
    Guid RestaurantId,
    decimal CreditLimit,
    decimal OutstandingBalance,
    decimal AvailableCredit,
    DateTime UpdatedAt);

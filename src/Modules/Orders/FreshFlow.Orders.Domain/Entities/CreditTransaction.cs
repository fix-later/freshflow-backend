using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class CreditTransaction
{
    private CreditTransaction() { } // EF Core

    public CreditTransaction(
        Guid restaurantId,
        Guid? orderId,
        CreditTransactionType type,
        decimal amount,
        decimal balanceAfter,
        string? note)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");

        if (balanceAfter < 0m)
            throw new ArgumentOutOfRangeException(nameof(balanceAfter), balanceAfter, "Balance after must be non-negative.");

        Id = Guid.NewGuid();
        RestaurantId = restaurantId;
        OrderId = orderId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid? OrderId { get; private set; }
    public CreditTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

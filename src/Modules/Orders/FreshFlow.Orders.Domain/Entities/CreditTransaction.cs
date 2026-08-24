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
        string? note,
        PaymentMethod? paymentMethod = null,
        string? reference = null,
        Guid? recordedByUserId = null)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");

        // AUDIT-2026-08-23 C3: BalanceAfter may be negative when the account is in credit
        // (FreshFlow owes the restaurant) — no lower-bound guard here.

        // paymentMethod/reference record HOW a debt payment was made — meaningless (and
        // therefore disallowed) outside a Settlement row.
        if (type != CreditTransactionType.Settlement &&
            (paymentMethod is not null || reference is not null || recordedByUserId is not null))
            throw new ArgumentException(
                "PaymentMethod, Reference, and RecordedByUserId are only applicable to Settlement transactions.");

        if (type == CreditTransactionType.Settlement &&
            (paymentMethod is null || string.IsNullOrWhiteSpace(reference) ||
             recordedByUserId is null || recordedByUserId == Guid.Empty))
            throw new ArgumentException(
                "PaymentMethod, Reference, and RecordedByUserId are required for Settlement transactions.");

        Id = Guid.NewGuid();
        RestaurantId = restaurantId;
        OrderId = orderId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        PaymentMethod = paymentMethod;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim().ToUpperInvariant();
        RecordedByUserId = recordedByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid? OrderId { get; private set; }
    public CreditTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Note { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
    public string? Reference { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

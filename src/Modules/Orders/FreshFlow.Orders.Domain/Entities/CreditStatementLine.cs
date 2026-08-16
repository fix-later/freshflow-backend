using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// A single frozen ledger entry captured into a <see cref="CreditStatement"/> snapshot at
/// generation time. Mirrors the source <see cref="CreditTransaction"/> fields needed for a
/// statement line and is never updated after creation — the statement it belongs to is
/// append-only.
/// </summary>
public sealed class CreditStatementLine
{
    private CreditStatementLine() { } // EF Core

    public CreditStatementLine(
        Guid transactionId,
        CreditTransactionType type,
        decimal amount,
        decimal balanceAfter,
        DateTime occurredAt,
        string? note,
        string? reference,
        Guid? orderId = null,
        PaymentMethod? paymentMethod = null)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));

        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");

        if (balanceAfter < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(balanceAfter), balanceAfter, "Balance after must be non-negative.");

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        OccurredAt = occurredAt;
        Note = note;
        Reference = reference;
        OrderId = orderId;
        PaymentMethod = paymentMethod;
    }

    public Guid Id { get; private set; }
    public Guid CreditStatementId { get; private set; }
    public Guid TransactionId { get; private set; }
    public CreditTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? Note { get; private set; }
    public string? Reference { get; private set; }
    public Guid? OrderId { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
}

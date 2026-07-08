namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// Immutable, append-only monthly credit statement snapshot. Computed ONCE at generation
/// time and frozen — a statement is never recomputed when viewed later, even if the
/// underlying ledger were somehow revised. Exactly one statement exists per
/// (RestaurantId, PeriodStart); the unique index in <c>CreditStatementConfiguration</c>
/// enforces this at the database level, and the application layer treats regeneration of
/// an already-generated period as returning the existing statement rather than an error.
/// </summary>
public sealed class CreditStatement
{
    private readonly List<CreditStatementLine> _lines = [];

    private CreditStatement() { } // EF Core

    public CreditStatement(
        Guid restaurantId,
        DateTime periodStart,
        DateTime periodEnd,
        decimal openingBalance,
        decimal totalCharges,
        decimal totalSettlements,
        decimal totalRefunds,
        IReadOnlyList<CreditStatementLine> lines)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (periodStart >= periodEnd)
            throw new ArgumentException("Period start must be earlier than period end.", nameof(periodStart));

        if (openingBalance < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(openingBalance), openingBalance, "Opening balance must be non-negative.");

        if (totalCharges < 0m || totalSettlements < 0m || totalRefunds < 0m)
            throw new ArgumentException("Period totals must be non-negative.");

        var closingBalance = openingBalance + totalCharges - totalSettlements - totalRefunds;
        if (closingBalance < 0m)
            throw new ArgumentException(
                "Computed closing balance cannot be negative — check the period totals.");

        Id = Guid.NewGuid();
        RestaurantId = restaurantId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        OpeningBalance = openingBalance;
        ClosingBalance = closingBalance;
        TotalCharges = totalCharges;
        TotalSettlements = totalSettlements;
        TotalRefunds = totalRefunds;
        GeneratedAt = DateTime.UtcNow;
        _lines.AddRange(lines);
    }

    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public decimal TotalCharges { get; private set; }
    public decimal TotalSettlements { get; private set; }
    public decimal TotalRefunds { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    public IReadOnlyCollection<CreditStatementLine> Lines => _lines.AsReadOnly();
}

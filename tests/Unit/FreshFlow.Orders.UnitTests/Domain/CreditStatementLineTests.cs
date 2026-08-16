using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class CreditStatementLineTests
{
    [Fact]
    public void Constructor_WithEmptyTransactionId_Throws()
    {
        var act = () => new CreditStatementLine(
            Guid.Empty, CreditTransactionType.Charge, 10m, 10m, DateTime.UtcNow, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        var act = () => new CreditStatementLine(
            Guid.NewGuid(), CreditTransactionType.Charge, 0m, 10m, DateTime.UtcNow, null, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeBalanceAfter_Throws()
    {
        var act = () => new CreditStatementLine(
            Guid.NewGuid(), CreditTransactionType.Charge, 10m, -1m, DateTime.UtcNow, null, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithValidInputs_SetsAllFields()
    {
        var transactionId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var line = new CreditStatementLine(
            transactionId, CreditTransactionType.Settlement, 50m, 25m, occurredAt, "Paid off", "TXN-1",
            orderId: null, PaymentMethod.BankTransfer);

        line.TransactionId.Should().Be(transactionId);
        line.Type.Should().Be(CreditTransactionType.Settlement);
        line.Amount.Should().Be(50m);
        line.BalanceAfter.Should().Be(25m);
        line.OccurredAt.Should().Be(occurredAt);
        line.Note.Should().Be("Paid off");
        line.Reference.Should().Be("TXN-1");
        line.OrderId.Should().BeNull();
        line.PaymentMethod.Should().Be(PaymentMethod.BankTransfer);
    }
}

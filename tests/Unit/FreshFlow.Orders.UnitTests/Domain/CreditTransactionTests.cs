using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class CreditTransactionTests
{
    [Fact]
    public void Constructor_TrimsBlankNoteToNull()
    {
        var transaction = new CreditTransaction(
            Guid.NewGuid(),
            orderId: null,
            CreditTransactionType.Settlement,
            amount: 100m,
            balanceAfter: 0m,
            note: "   ");

        transaction.Note.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        var act = () => new CreditTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreditTransactionType.Charge,
            amount: 0m,
            balanceAfter: 0m,
            note: null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeBalanceAfter_Throws()
    {
        var act = () => new CreditTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreditTransactionType.Refund,
            amount: 10m,
            balanceAfter: -1m,
            note: null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

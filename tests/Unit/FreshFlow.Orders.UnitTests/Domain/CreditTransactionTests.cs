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

    [Fact]
    public void Constructor_SettlementWithPaymentMethodAndReference_SetsBoth()
    {
        var transaction = new CreditTransaction(
            Guid.NewGuid(),
            orderId: null,
            CreditTransactionType.Settlement,
            amount: 100m,
            balanceAfter: 0m,
            note: "Paid off",
            paymentMethod: PaymentMethod.BankTransfer,
            reference: "TXN-123456");

        transaction.PaymentMethod.Should().Be(PaymentMethod.BankTransfer);
        transaction.Reference.Should().Be("TXN-123456");
    }

    [Fact]
    public void Constructor_NonSettlementWithPaymentMethod_Throws()
    {
        var act = () => new CreditTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreditTransactionType.Charge,
            amount: 100m,
            balanceAfter: 100m,
            note: null,
            paymentMethod: PaymentMethod.Manual);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NonSettlementWithReference_Throws()
    {
        var act = () => new CreditTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreditTransactionType.Refund,
            amount: 10m,
            balanceAfter: 0m,
            note: null,
            reference: "TXN-999");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_BlankReference_TrimsToNull()
    {
        var transaction = new CreditTransaction(
            Guid.NewGuid(),
            orderId: null,
            CreditTransactionType.Settlement,
            amount: 50m,
            balanceAfter: 0m,
            note: null,
            paymentMethod: PaymentMethod.Manual,
            reference: "   ");

        transaction.Reference.Should().BeNull();
    }
}

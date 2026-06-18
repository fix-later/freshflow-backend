using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class RestaurantCreditTests
{
    [Fact]
    public void Constructor_WithNegativeLimit_Throws()
    {
        var act = () => new RestaurantCredit(Guid.NewGuid(), creditLimit: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Charge_WithinLimit_IncreasesOutstandingBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        credit.Charge(40m);

        credit.OutstandingBalance.Should().Be(40m);
        credit.AvailableCredit.Should().Be(60m);
    }

    [Fact]
    public void Charge_ExceedingLimit_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        var act = () => credit.Charge(101m);

        act.Should().Throw<InvalidOperationException>();
        credit.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public void Settle_WithinOutstandingBalance_DecreasesBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(80m);

        credit.Settle(30m);

        credit.OutstandingBalance.Should().Be(50m);
        credit.AvailableCredit.Should().Be(50m);
    }

    [Fact]
    public void Settle_ExceedingOutstandingBalance_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(20m);

        var act = () => credit.Settle(21m);

        act.Should().Throw<InvalidOperationException>();
        credit.OutstandingBalance.Should().Be(20m);
    }

    [Fact]
    public void Refund_WithinOutstandingBalance_DecreasesBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(70m);

        credit.Refund(25m);

        credit.OutstandingBalance.Should().Be(45m);
    }

    [Fact]
    public void SetCreditLimit_NonNegativeValue_UpdatesLimit()
    {
        var credit = new RestaurantCredit(Guid.NewGuid());

        credit.SetCreditLimit(500m);

        credit.CreditLimit.Should().Be(500m);
    }

    [Fact]
    public void SetCreditLimit_Zero_AllowsRevokingCredit()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 500m);

        credit.SetCreditLimit(0m);

        credit.CreditLimit.Should().Be(0m);
    }

    [Fact]
    public void SetCreditLimit_NegativeValue_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid());

        var act = () => credit.SetCreditLimit(-1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetCreditLimit_BelowOutstandingBalance_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(80m);

        var act = () => credit.SetCreditLimit(50m);

        act.Should().Throw<InvalidOperationException>();
        credit.CreditLimit.Should().Be(100m);
    }
}

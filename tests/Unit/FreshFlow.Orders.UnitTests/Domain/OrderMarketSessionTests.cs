using FluentAssertions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrderMarketSessionTests
{
    [Fact]
    public void AddItem_RejectsDifferentMarket_AndEmptyCartResetsMarket()
    {
        var order = new Order(Guid.NewGuid(), null, null);
        var marketId = Guid.NewGuid();
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 10m, marketId).IsSuccess.Should().BeTrue();

        order.AddItem(Guid.NewGuid(), "Fish", 1, 20m, Guid.NewGuid())
            .Error.Code.Should().Be("ORDER_MARKET_MISMATCH");
        order.RemoveItem(order.Items.Single().Id).IsSuccess.Should().BeTrue();
        order.MarketId.Should().BeNull();
    }

    [Fact]
    public void GetServiceDate_UsesVietnamDateNearUtcMidnight()
    {
        OrderCutoffScheduler.GetServiceDate(
                new DateTime(2026, 8, 13, 18, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 8, 14));
    }

    [Fact]
    public void Confirm_PersistsSessionMembershipAndTimestamp()
    {
        var sessionId = Guid.NewGuid();
        var confirmedAt = new DateTime(2026, 8, 13, 3, 15, 0, DateTimeKind.Utc);
        var order = new Order(Guid.NewGuid(), confirmedAt.AddDays(1), null);
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 10m);

        var result = order.Confirm(sessionId, confirmedAt);

        result.IsSuccess.Should().BeTrue();
        order.MarketSessionId.Should().Be(sessionId);
        order.ConfirmedAt.Should().Be(confirmedAt);
    }
}

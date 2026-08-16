using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.EventHandlers;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementPurchaseConfirmedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesMarketPriceAndAddsSnapshotAsync()
    {
        var marketId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var marketProduct = new MarketProduct(marketId, Guid.NewGuid(), 10_000m, 5, null);
        var marketProducts = Substitute.For<IMarketProductRepository>();
        marketProducts.FindByIdAsync(marketProduct.Id, default).Returns(marketProduct);
        var snapshots = Substitute.For<IPriceSnapshotRepository>();
        var handler = new ProcurementPurchaseConfirmedIntegrationEventHandler(
            marketProducts,
            snapshots,
            Substitute.For<ILogger<ProcurementPurchaseConfirmedIntegrationEventHandler>>());

        await handler.Handle(
            new ProcurementPurchaseConfirmedIntegrationEvent(
                Guid.NewGuid(),
                marketId,
                agentId,
                new Dictionary<Guid, decimal> { [marketProduct.Id] = 12_000m },
                DateTime.UtcNow),
            default);

        marketProduct.CurrentPrice.Should().Be(12_000m);
        marketProduct.UpdatedBy.Should().Be(agentId);
        await snapshots.Received(1).AddAsync(
            Arg.Is<PriceSnapshot>(snapshot =>
                snapshot.MarketProductId == marketProduct.Id &&
                snapshot.Price == 12_000m &&
                snapshot.RecordedBy == agentId),
            default);
        await marketProducts.Received(1).SaveChangesAsync(default);
    }
}

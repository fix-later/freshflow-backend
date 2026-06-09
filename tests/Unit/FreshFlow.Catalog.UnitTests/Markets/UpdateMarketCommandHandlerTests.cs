using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Update;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class UpdateMarketCommandHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly UpdateMarketCommandHandler _sut;

    public UpdateMarketCommandHandlerTests()
    {
        _sut = new UpdateMarketCommandHandler(_markets);
    }

    [Fact]
    public async Task Handle_ExistingMarket_UpdatesAndReturnsDto()
    {
        // Arrange
        var market = new Market("Old Name", null, null, null, null);
        var id = market.Id;
        _markets.FindByIdAsync(id, default).Returns(market);

        var cmd = new UpdateMarketCommand(id, "New Name", "New Location", "New Address", 10.5m, 106.5m);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Name");
        result.Value.Location.Should().Be("New Location");
        await _markets.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentMarket_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _markets.FindByIdAsync(id, default).Returns((Market?)null);

        var cmd = new UpdateMarketCommand(id, "Name", null, null, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        await _markets.DidNotReceive().SaveChangesAsync(default);
    }
}

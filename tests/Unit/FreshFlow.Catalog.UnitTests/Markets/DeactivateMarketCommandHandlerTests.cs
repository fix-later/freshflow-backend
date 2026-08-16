using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Deactivate;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class DeactivateMarketCommandHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly DeactivateMarketCommandHandler _sut;

    public DeactivateMarketCommandHandlerTests()
    {
        _sut = new DeactivateMarketCommandHandler(_markets);
    }

    [Fact]
    public async Task Handle_ActiveMarket_DeactivatesAndReturnsDto()
    {
        // Arrange
        var market = new Market("Thủ Đức Market", null, null, null, null);
        var id = market.Id;
        market.IsActive.Should().BeTrue(); // pre-condition
        _markets.FindByIdAsync(id, default).Returns(market);

        // Act
        var result = await _sut.Handle(new DeactivateMarketCommand(id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        await _markets.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentMarket_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _markets.FindByIdAsync(id, default).Returns((Market?)null);

        // Act
        var result = await _sut.Handle(new DeactivateMarketCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        await _markets.DidNotReceive().SaveChangesAsync(default);
    }
}

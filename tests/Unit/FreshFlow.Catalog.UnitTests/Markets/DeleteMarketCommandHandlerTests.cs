using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Delete;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class DeleteMarketCommandHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly DeleteMarketCommandHandler _sut;

    public DeleteMarketCommandHandlerTests()
    {
        _sut = new DeleteMarketCommandHandler(_markets);
    }

    [Fact]
    public async Task Handle_ExistingMarket_SoftDeletesAndReturnsSuccess()
    {
        // Arrange
        var market = new Market("Hóc Môn Market", null, null, null, null);
        var id = market.Id;
        _markets.FindByIdAsync(id, default).Returns(market);

        // Act
        var result = await _sut.Handle(new DeleteMarketCommand(id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        market.IsDeleted.Should().BeTrue();
        market.IsActive.Should().BeFalse();
        await _markets.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentMarket_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _markets.FindByIdAsync(id, default).Returns((Market?)null);

        // Act
        var result = await _sut.Handle(new DeleteMarketCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        await _markets.DidNotReceive().SaveChangesAsync(default);
    }
}

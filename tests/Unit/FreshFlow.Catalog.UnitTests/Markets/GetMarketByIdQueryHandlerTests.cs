using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarketById;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class GetMarketByIdQueryHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly GetMarketByIdQueryHandler _sut;

    public GetMarketByIdQueryHandlerTests()
    {
        _sut = new GetMarketByIdQueryHandler(_markets);
    }

    [Fact]
    public async Task Handle_ExistingMarket_ReturnsDto()
    {
        // Arrange
        var market = new Market("Thủ Đức Market", "Thủ Đức", "456 Road", 10.85m, 106.75m);
        _markets.FindByIdAsync(market.Id, default).Returns(market);

        // Act
        var result = await _sut.Handle(new GetMarketByIdQuery(market.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(market.Id);
        result.Value.Name.Should().Be("Thủ Đức Market");
        result.Value.Location.Should().Be("Thủ Đức");
    }

    [Fact]
    public async Task Handle_NonExistentMarket_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _markets.FindByIdAsync(id, default).Returns((Market?)null);

        // Act
        var result = await _sut.Handle(new GetMarketByIdQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
    }
}

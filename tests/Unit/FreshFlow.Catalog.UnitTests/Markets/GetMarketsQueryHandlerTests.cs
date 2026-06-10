using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class GetMarketsQueryHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly GetMarketsQueryHandler _sut;

    public GetMarketsQueryHandlerTests()
    {
        _sut = new GetMarketsQueryHandler(_markets);
    }

    [Fact]
    public async Task Handle_ActiveOnly_ReturnsActiveMarkets()
    {
        // Arrange
        IReadOnlyList<Market> marketList =
        [
            new Market("Hóc Môn", "Hóc Môn", null, null, null),
            new Market("Bình Điền", "Bình Điền", null, null, null),
        ];
        _markets.GetAllAsync(true, default).Returns(marketList);

        // Act
        var result = await _sut.Handle(new GetMarketsQuery(ActiveOnly: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(m => m.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_AllMarkets_DelegatesToRepository()
    {
        // Arrange
        IReadOnlyList<Market> marketList = [];
        _markets.GetAllAsync(false, default).Returns(marketList);

        // Act
        var result = await _sut.Handle(new GetMarketsQuery(ActiveOnly: false), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await _markets.Received(1).GetAllAsync(false, default);
    }
}

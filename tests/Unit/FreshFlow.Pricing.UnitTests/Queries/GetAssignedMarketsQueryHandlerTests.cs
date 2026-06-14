using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Application.Queries.GetAssignedMarkets;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetAssignedMarketsQueryHandlerTests
{
    private readonly IAssignedMarketReader _reader = Substitute.For<IAssignedMarketReader>();
    private readonly GetAssignedMarketsQueryHandler _sut;

    public GetAssignedMarketsQueryHandlerTests()
    {
        _sut = new GetAssignedMarketsQueryHandler(_reader);
    }

    [Fact]
    public async Task Handle_AgentWithAssignedMarkets_ReturnsMarketListAsync()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var marketId1 = Guid.NewGuid();
        var marketId2 = Guid.NewGuid();

        IReadOnlyList<AssignedMarketDto> markets =
        [
            new(marketId1, "Hóc Môn Market", "Hóc Môn", "123 Street"),
            new(marketId2, "Bình Điền Market", "Bình Điền", null),
        ];
        _reader.GetByAgentIdAsync(agentId, default).Returns(markets);

        // Act
        var result = await _sut.Handle(new GetAssignedMarketsQuery(agentId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].MarketId.Should().Be(marketId1);
        result.Value[0].Name.Should().Be("Hóc Môn Market");
        result.Value[1].MarketId.Should().Be(marketId2);
    }

    [Fact]
    public async Task Handle_AgentWithNoAssignedMarkets_ReturnsEmptyListAsync()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _reader.GetByAgentIdAsync(agentId, default).Returns([]);

        // Act
        var result = await _sut.Handle(new GetAssignedMarketsQuery(agentId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DelegatesToReader_WithCorrectUserIdAsync()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _reader.GetByAgentIdAsync(agentId, default).Returns([]);

        // Act
        await _sut.Handle(new GetAssignedMarketsQuery(agentId), default);

        // Assert
        await _reader.Received(1).GetByAgentIdAsync(agentId, default);
    }

    [Fact]
    public async Task Handle_DtoFields_AreMappedCorrectlyAsync()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var marketId = Guid.NewGuid();

        IReadOnlyList<AssignedMarketDto> markets =
        [
            new(marketId, "Central Market", "District 1", "45 Main St"),
        ];
        _reader.GetByAgentIdAsync(agentId, default).Returns(markets);

        // Act
        var result = await _sut.Handle(new GetAssignedMarketsQuery(agentId), default);

        // Assert
        var dto = result.Value.Single();
        dto.MarketId.Should().Be(marketId);
        dto.Name.Should().Be("Central Market");
        dto.Location.Should().Be("District 1");
        dto.Address.Should().Be("45 Main St");
    }
}

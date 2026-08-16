using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Commands.CreateHub;
using FreshFlow.Hub.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateHubCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesActiveHubAsync()
    {
        var repository = new InMemoryHubRepository();
        var marketId = Guid.NewGuid();
        var markets = ActiveMarketReader(marketId);
        var sut = new CreateHubCommandHandler(repository, markets);
        var managedBy = Guid.NewGuid();

        var result = await sut.Handle(
            new CreateHubCommand(marketId, " Main Hub ", " 123 Road ", 10m, 106m, 1000, managedBy),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().NotBeEmpty();
        result.Value.MarketId.Should().Be(marketId);
        result.Value.Name.Should().Be("Main Hub");
        result.Value.Address.Should().Be("123 Road");
        result.Value.CapacityKg.Should().Be(1000);
        result.Value.OccupiedCapacityKg.Should().Be(0);
        result.Value.AvailableCapacityKg.Should().Be(1000);
        result.Value.IsActive.Should().BeTrue();
        result.Value.ManagedBy.Should().Be(managedBy);
        repository.Hubs.Should().ContainSingle();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SaveConcurrencyConflict_ReturnsConflictAsync()
    {
        var repository = new InMemoryHubRepository { ThrowConcurrencyOnSave = true };
        var marketId = Guid.NewGuid();
        var sut = new CreateHubCommandHandler(repository, ActiveMarketReader(marketId));

        var result = await sut.Handle(
            new CreateHubCommand(marketId, "Main Hub", null, null, null, 1000, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Theory]
    [InlineData(false, false, "MARKET_NOT_FOUND")]
    [InlineData(true, false, "MARKET_INACTIVE")]
    public async Task Handle_UnavailableMarket_RejectsBeforeCreatingHubAsync(
        bool exists,
        bool isActive,
        string expectedCode)
    {
        var repository = new InMemoryHubRepository();
        var marketId = Guid.NewGuid();
        var markets = Substitute.For<IMarketReader>();
        markets.FindAsync(marketId, default)
            .Returns(exists ? new MarketSnapshot(marketId, isActive) : null);

        var result = await new CreateHubCommandHandler(repository, markets).Handle(
            new CreateHubCommand(marketId, "Main Hub", null, null, null, 1000, null),
            default);

        result.Error.Code.Should().Be(expectedCode);
        repository.Hubs.Should().BeEmpty();
    }

    private static IMarketReader ActiveMarketReader(Guid marketId)
    {
        var reader = Substitute.For<IMarketReader>();
        reader.FindAsync(marketId, default)
            .Returns(new MarketSnapshot(marketId, true));
        return reader;
    }

}

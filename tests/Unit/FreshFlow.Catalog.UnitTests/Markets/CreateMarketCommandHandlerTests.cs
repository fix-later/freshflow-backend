using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class CreateMarketCommandHandlerTests
{
    private readonly IMarketRepository _markets = Substitute.For<IMarketRepository>();
    private readonly CreateMarketCommandHandler _sut;

    public CreateMarketCommandHandlerTests()
    {
        _sut = new CreateMarketCommandHandler(_markets);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesAndReturnsMarket()
    {
        // Arrange
        var cmd = new CreateMarketCommand("Hóc Môn Market", "Hóc Môn", "123 Street", 10.8m, 106.6m);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Hóc Môn Market");
        result.Value.Location.Should().Be("Hóc Môn");
        result.Value.IsActive.Should().BeTrue();

        await _markets.Received(1).AddAsync(Arg.Is<Market>(m => m.Name == "Hóc Môn Market"), default);
        await _markets.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NullOptionalFields_StillSucceeds()
    {
        // Arrange
        var cmd = new CreateMarketCommand("Bình Điền Market", null, null, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().BeNull();
        result.Value.Address.Should().BeNull();
        result.Value.Latitude.Should().BeNull();
        result.Value.Longitude.Should().BeNull();
    }
}

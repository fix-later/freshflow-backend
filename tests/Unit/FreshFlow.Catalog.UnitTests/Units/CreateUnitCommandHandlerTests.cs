using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Units.Create;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class CreateUnitCommandHandlerTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly CreateUnitCommandHandler _sut;

    public CreateUnitCommandHandlerTests()
    {
        _sut = new CreateUnitCommandHandler(_units);
    }

    [Fact]
    public async Task Handle_UniqueNameWithAbbreviation_ReturnsDto()
    {
        // Arrange
        _units.ExistsByNameAsync("kg", default).Returns(false);

        // Act
        var result = await _sut.Handle(new CreateUnitCommand("kg", "kg"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("kg");
        result.Value.Abbreviation.Should().Be("kg");
        result.Value.IsActive.Should().BeTrue();
        await _units.Received(1).AddAsync(Arg.Any<FreshFlow.Catalog.Domain.Entities.UnitOfMeasurement>(), default);
        await _units.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UniqueNameNoAbbreviation_ReturnsDto()
    {
        // Arrange
        _units.ExistsByNameAsync("thùng", default).Returns(false);

        // Act
        var result = await _sut.Handle(new CreateUnitCommand("thùng", null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("thùng");
        result.Value.Abbreviation.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        // Arrange
        _units.ExistsByNameAsync("kg", default).Returns(true);

        // Act
        var result = await _sut.Handle(new CreateUnitCommand("kg", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNIT_NAME_CONFLICT");
        await _units.DidNotReceive().AddAsync(Arg.Any<FreshFlow.Catalog.Domain.Entities.UnitOfMeasurement>(), default);
    }
}

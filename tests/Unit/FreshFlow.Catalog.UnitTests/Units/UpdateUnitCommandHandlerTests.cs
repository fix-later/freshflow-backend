using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Units.Update;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class UpdateUnitCommandHandlerTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly UpdateUnitCommandHandler _sut;

    public UpdateUnitCommandHandlerTests()
    {
        _sut = new UpdateUnitCommandHandler(_units);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ReturnsUpdatedDto()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _units.ExistsByNameAsync("kilogram", default).Returns(false);

        // Act
        var result = await _sut.Handle(new UpdateUnitCommand(unit.Id, "kilogram", "kG"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("kilogram");
        result.Value.Abbreviation.Should().Be("kG");
        await _units.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SameNameCaseInsensitive_SkipsDuplicateCheck()
    {
        // Arrange
        var unit = new UnitOfMeasurement("KG", "kg");
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        // Act
        var result = await _sut.Handle(new UpdateUnitCommand(unit.Id, "kg", "kg"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _units.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _units.FindByIdAsync(id, default).Returns((UnitOfMeasurement?)null);

        // Act
        var result = await _sut.Handle(new UpdateUnitCommand(id, "kg", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNITOFMEASUREMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _units.ExistsByNameAsync("thùng", default).Returns(true);

        // Act
        var result = await _sut.Handle(new UpdateUnitCommand(unit.Id, "thùng", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNIT_NAME_CONFLICT");
    }
}

using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Units.Deactivate;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class DeactivateUnitCommandHandlerTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly DeactivateUnitCommandHandler _sut;

    public DeactivateUnitCommandHandlerTests()
    {
        _sut = new DeactivateUnitCommandHandler(_units);
    }

    [Fact]
    public async Task Handle_ExistingUnit_DeactivatesAndReturnsDto()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        // Act
        var result = await _sut.Handle(new DeactivateUnitCommand(unit.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        await _units.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _units.FindByIdAsync(id, default).Returns((UnitOfMeasurement?)null);

        // Act
        var result = await _sut.Handle(new DeactivateUnitCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNITOFMEASUREMENT_NOT_FOUND");
    }
}

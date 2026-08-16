using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Units.GetUnitById;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class GetUnitByIdQueryHandlerTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly GetUnitByIdQueryHandler _sut;

    public GetUnitByIdQueryHandlerTests()
    {
        _sut = new GetUnitByIdQueryHandler(_units);
    }

    [Fact]
    public async Task Handle_ExistingUnit_ReturnsDto()
    {
        // Arrange
        var unit = new UnitOfMeasurement("bó", "bó");
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        // Act
        var result = await _sut.Handle(new GetUnitByIdQuery(unit.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(unit.Id);
        result.Value.Name.Should().Be("bó");
        result.Value.Abbreviation.Should().Be("bó");
    }

    [Fact]
    public async Task Handle_NonExistentUnit_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _units.FindByIdAsync(id, default).Returns((UnitOfMeasurement?)null);

        // Act
        var result = await _sut.Handle(new GetUnitByIdQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNITOFMEASUREMENT_NOT_FOUND");
    }
}

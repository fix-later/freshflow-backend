using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Units.GetUnits;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class GetUnitsQueryHandlerTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly GetUnitsQueryHandler _sut;

    public GetUnitsQueryHandlerTests()
    {
        _sut = new GetUnitsQueryHandler(_units);
    }

    [Fact]
    public async Task Handle_ActiveOnly_ReturnsActiveUnits()
    {
        // Arrange
        IReadOnlyList<UnitOfMeasurement> list =
        [
            new UnitOfMeasurement("kg", "kg"),
            new UnitOfMeasurement("thùng", null),
        ];
        _units.GetAllAsync(true, default).Returns(list);

        // Act
        var result = await _sut.Handle(new GetUnitsQuery(ActiveOnly: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_IncludeInactive_DelegatesToRepository()
    {
        // Arrange
        IReadOnlyList<UnitOfMeasurement> list = [];
        _units.GetAllAsync(false, default).Returns(list);

        // Act
        var result = await _sut.Handle(new GetUnitsQuery(ActiveOnly: false), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _units.Received(1).GetAllAsync(false, default);
    }
}

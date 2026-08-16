using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.ListVehicles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListVehiclesQueryValidatorTests
{
    private readonly ListVehiclesQueryValidator _sut = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _sut.Validate(new ListVehiclesQuery(PageSize: 50));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void Validate_PageSizeOutsideRange_Fails(int pageSize)
    {
        var result = _sut.Validate(new ListVehiclesQuery(PageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }
}

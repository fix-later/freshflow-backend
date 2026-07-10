using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.ListRoutes;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListRoutesQueryValidatorTests
{
    private readonly ListRoutesQueryValidator _sut = new();

    [Fact]
    public void Query_DefaultPageSize_Is50()
    {
        var query = new ListRoutesQuery(null);

        query.PageSize.Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void Validate_PageSizeOutsideRange_Fails(int pageSize)
    {
        var result = _sut.Validate(new ListRoutesQuery(null, pageSize));

        result.IsValid.Should().BeFalse();
    }
}

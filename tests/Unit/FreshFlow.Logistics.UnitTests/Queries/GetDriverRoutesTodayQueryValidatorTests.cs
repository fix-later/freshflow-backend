using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDriverRoutesTodayQueryValidatorTests
{
    [Fact]
    public void Validate_EmptyDriverUserId_Fails()
    {
        var sut = new GetDriverRoutesTodayQueryValidator();

        var result = sut.Validate(new GetDriverRoutesTodayQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}

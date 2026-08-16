using FluentAssertions;
using FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetHubOrdersByRestaurantQueryValidatorTests
{
    [Fact]
    public void Validate_EmptyHubId_Fails()
    {
        var result = new GetHubOrdersByRestaurantQueryValidator()
            .Validate(new GetHubOrdersByRestaurantQuery(Guid.Empty, new DateOnly(2026, 7, 29)));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_OutOfRangeServiceDate_Fails(bool useMaxValue)
    {
        var serviceDate = useMaxValue ? DateOnly.MaxValue : default;

        var result = new GetHubOrdersByRestaurantQueryValidator()
            .Validate(new GetHubOrdersByRestaurantQuery(Guid.NewGuid(), serviceDate));

        result.IsValid.Should().BeFalse();
    }
}

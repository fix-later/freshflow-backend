using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRouteSuggestionsQueryValidatorTests
{
    [Fact]
    public void Validate_MissingServiceDate_Fails()
    {
        var result = new GetRouteSuggestionsQueryValidator()
            .Validate(new GetRouteSuggestionsQuery(default));

        result.IsValid.Should().BeFalse();
    }
}

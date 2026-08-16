using FluentAssertions;
using FreshFlow.Orders.Application.Queries.ListCreditStatements;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListCreditStatementsQueryValidatorTests
{
    private readonly ListCreditStatementsQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidQuery_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new ListCreditStatementsQuery(Guid.NewGuid(), false, Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new ListCreditStatementsQuery(Guid.Empty, false, Guid.NewGuid()));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListCreditStatementsQuery.UserId));
    }

    [Fact]
    public async Task Validate_EmptyRestaurantId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new ListCreditStatementsQuery(Guid.NewGuid(), false, Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListCreditStatementsQuery.RestaurantId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task Validate_PageSizeOutOfRange_FailsAsync(int pageSize)
    {
        var result = await _sut.ValidateAsync(
            new ListCreditStatementsQuery(Guid.NewGuid(), false, Guid.NewGuid(), PageSize: pageSize));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListCreditStatementsQuery.PageSize));
    }
}

using FluentAssertions;
using FreshFlow.Auth.Application.Queries.GetUsers;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _sut = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_InvalidPage_Fails(int page)
    {
        var result = await _sut.ValidateAsync(new GetUsersQuery(null, null, null, Page: page, PageSize: 20));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetUsersQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public async Task Validate_InvalidPageSize_Fails(int pageSize)
    {
        var result = await _sut.ValidateAsync(new GetUsersQuery(null, null, null, Page: 1, PageSize: pageSize));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetUsersQuery.PageSize));
    }

    [Fact]
    public async Task Validate_ValidQuery_Passes()
    {
        var result = await _sut.ValidateAsync(new GetUsersQuery(null, null, null, Page: 1, PageSize: 20));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("active")]
    [InlineData("suspended")]
    [InlineData("PENDING")]
    public async Task Validate_ValidRestaurantStatus_Passes(string status)
    {
        var result = await _sut.ValidateAsync(
            new GetUsersQuery(null, null, null, Page: 1, PageSize: 20, RestaurantStatus: status));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_InvalidRestaurantStatus_Fails()
    {
        var result = await _sut.ValidateAsync(
            new GetUsersQuery(null, null, null, Page: 1, PageSize: 20, RestaurantStatus: "rejected"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetUsersQuery.RestaurantStatus));
    }
}

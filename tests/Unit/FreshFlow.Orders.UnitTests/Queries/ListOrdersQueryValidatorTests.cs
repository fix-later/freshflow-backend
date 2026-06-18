using FluentAssertions;
using FreshFlow.Orders.Application.Queries.ListOrders;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListOrdersQueryValidatorTests
{
    private readonly ListOrdersQueryValidator _sut = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_InvalidPage_FailsAsync(int page)
    {
        var result = await _sut.ValidateAsync(ValidQuery() with { Page = page });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validate_InvalidPageSize_FailsAsync(int pageSize)
    {
        var result = await _sut.ValidateAsync(ValidQuery() with { PageSize = pageSize });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.PageSize));
    }

    [Fact]
    public async Task Validate_InvalidStatus_FailsAsync()
    {
        var result = await _sut.ValidateAsync(ValidQuery() with { Status = "unknown" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.Status));
    }

    [Fact]
    public async Task Validate_InvalidSort_FailsAsync()
    {
        var result = await _sut.ValidateAsync(ValidQuery() with { Sort = "totalAmount:desc" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.Sort));
    }

    [Fact]
    public async Task Validate_FromAfterTo_FailsAsync()
    {
        var result = await _sut.ValidateAsync(ValidQuery() with
        {
            From = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc)
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "'From' must be before or equal to 'To'.");
    }

    [Fact]
    public async Task Validate_ToDateOnly_CoversEntireDayAsync()
    {
        var result = await _sut.ValidateAsync(ValidQuery() with
        {
            From = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("confirmed")]
    [InlineData("picked_up")]
    [InlineData("in_transit")]
    public async Task Validate_ValidStatusValues_PassAsync(string? status)
    {
        var result = await _sut.ValidateAsync(ValidQuery() with { Status = status });

        result.IsValid.Should().BeTrue();
    }

    private static ListOrdersQuery ValidQuery() =>
        new(
            Guid.NewGuid(),
            IsAdmin: false,
            RestaurantId: null,
            Status: null,
            From: null,
            To: null);
}

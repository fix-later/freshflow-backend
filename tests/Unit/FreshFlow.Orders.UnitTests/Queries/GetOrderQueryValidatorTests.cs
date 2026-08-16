using FluentAssertions;
using FreshFlow.Orders.Application.Queries.GetOrder;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetOrderQueryValidatorTests
{
    private readonly GetOrderQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidQuery_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new GetOrderQuery(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new GetOrderQuery(Guid.Empty, IsAdmin: false, Guid.NewGuid()));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetOrderQuery.UserId));
    }

    [Fact]
    public async Task Validate_EmptyOrderId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new GetOrderQuery(Guid.NewGuid(), IsAdmin: false, Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetOrderQuery.OrderId));
    }
}

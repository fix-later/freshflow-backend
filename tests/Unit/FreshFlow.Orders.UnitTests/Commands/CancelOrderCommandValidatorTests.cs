using FluentAssertions;
using FreshFlow.Orders.Application.Commands.CancelOrder;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CancelOrderCommandValidatorTests
{
    private readonly CancelOrderCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new CancelOrderCommand(
            Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), Reason: "Khách hàng đổi ý"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new CancelOrderCommand(
            Guid.Empty, IsAdmin: false, Guid.NewGuid(), Reason: null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelOrderCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyOrderId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new CancelOrderCommand(
            Guid.NewGuid(), IsAdmin: false, Guid.Empty, Reason: null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelOrderCommand.OrderId));
    }

    [Fact]
    public async Task Validate_ReasonLongerThan500_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new CancelOrderCommand(
            Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), new string('x', 501)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelOrderCommand.Reason));
    }
}

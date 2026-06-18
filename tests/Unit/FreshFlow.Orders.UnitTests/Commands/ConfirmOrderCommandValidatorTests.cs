using FluentAssertions;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmOrderCommandValidatorTests
{
    private readonly ConfirmOrderCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new ConfirmOrderCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new ConfirmOrderCommand(Guid.Empty, Guid.NewGuid()));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConfirmOrderCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyOrderId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new ConfirmOrderCommand(Guid.NewGuid(), Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConfirmOrderCommand.OrderId));
    }
}

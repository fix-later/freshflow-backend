using FluentAssertions;
using FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RecordOrderItemActualQuantityCommandValidatorTests
{
    private readonly RecordOrderItemActualQuantityCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new RecordOrderItemActualQuantityCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4.5m));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new RecordOrderItemActualQuantityCommand(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 4m));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordOrderItemActualQuantityCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyOrderId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new RecordOrderItemActualQuantityCommand(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 4m));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordOrderItemActualQuantityCommand.OrderId));
    }

    [Fact]
    public async Task Validate_EmptyOrderItemId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new RecordOrderItemActualQuantityCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 4m));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordOrderItemActualQuantityCommand.OrderItemId));
    }

    [Fact]
    public async Task Validate_NegativeActualQuantity_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new RecordOrderItemActualQuantityCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RecordOrderItemActualQuantityCommand.ActualQuantity));
    }
}

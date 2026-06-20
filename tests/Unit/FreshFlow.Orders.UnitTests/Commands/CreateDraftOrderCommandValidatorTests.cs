using FluentAssertions;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Dtos;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateDraftOrderCommandValidatorTests
{
    private readonly CreateDraftOrderCommandValidator _sut = new();

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        // Arrange
        var cmd = new CreateDraftOrderCommand(
            Guid.Empty, [new DraftOrderItemRequest(Guid.NewGuid(), 1)], null, null);

        // Act
        var result = _sut.Validate(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyItemsList_Fails()
    {
        // Arrange
        var cmd = new CreateDraftOrderCommand(Guid.NewGuid(), [], null, null);

        // Act
        var result = _sut.Validate(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemWithEmptyMarketProductId_Fails()
    {
        // Arrange
        var cmd = new CreateDraftOrderCommand(
            Guid.NewGuid(), [new DraftOrderItemRequest(Guid.Empty, 1)], null, null);

        // Act
        var result = _sut.Validate(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ItemWithZeroOrNegativeQuantity_Fails(int quantity)
    {
        // Arrange
        var cmd = new CreateDraftOrderCommand(
            Guid.NewGuid(), [new DraftOrderItemRequest(Guid.NewGuid(), quantity)], null, null);

        // Act
        var result = _sut.Validate(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var cmd = new CreateDraftOrderCommand(
            Guid.NewGuid(), [new DraftOrderItemRequest(Guid.NewGuid(), 5)], DateTime.UtcNow.AddDays(3), "notes");

        // Act
        var result = _sut.Validate(cmd);

        // Assert
        result.IsValid.Should().BeTrue();
    }

}

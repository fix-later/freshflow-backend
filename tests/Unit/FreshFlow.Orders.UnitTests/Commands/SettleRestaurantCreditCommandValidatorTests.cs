using FluentAssertions;
using FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SettleRestaurantCreditCommandValidatorTests
{
    private readonly SettleRestaurantCreditCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(Guid.NewGuid(), 100m, "payment"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NonPositiveAmount_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(Guid.NewGuid(), 0m, null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.Amount));
    }

    [Fact]
    public async Task Validate_LongNote_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(Guid.NewGuid(), 1m, new string('x', 501)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.Note));
    }
}

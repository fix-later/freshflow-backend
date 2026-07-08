using FluentAssertions;
using FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SettleRestaurantCreditCommandValidatorTests
{
    private readonly SettleRestaurantCreditCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new SettleRestaurantCreditCommand(Guid.NewGuid(), 100m, PaymentMethod.BankTransfer, "TXN-1", "payment"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NonPositiveAmount_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new SettleRestaurantCreditCommand(Guid.NewGuid(), 0m, PaymentMethod.Manual, null, null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.Amount));
    }

    [Fact]
    public async Task Validate_LongNote_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(
            Guid.NewGuid(), 1m, PaymentMethod.Manual, null, new string('x', 501)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.Note));
    }

    [Fact]
    public async Task Validate_InvalidPaymentMethod_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(
            Guid.NewGuid(), 1m, (PaymentMethod)999, null, null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.PaymentMethod));
    }

    [Fact]
    public async Task Validate_LongReference_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new SettleRestaurantCreditCommand(
            Guid.NewGuid(), 1m, PaymentMethod.BankTransfer, new string('x', 201), null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SettleRestaurantCreditCommand.Reference));
    }
}

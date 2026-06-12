using FluentAssertions;
using FreshFlow.Pricing.Application.Commands.UpdateProductPrice;
using FreshFlow.Pricing.Application.Options;
using Microsoft.Extensions.Options;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateProductPriceCommandValidatorTests
{
    private const decimal MaxPrice = 10_000_000m; // test threshold

    private static readonly IOptions<PricingOptions> Opts =
        Options.Create(new PricingOptions { MaxPriceVnd = MaxPrice });

    private readonly UpdateProductPriceCommandValidator _sut = new(Opts);

    private static UpdateProductPriceCommand Valid(
        decimal? price = 125_000m,
        int? quantity = null,
        DateTime? expectedVersion = null) =>
        new(
            MarketId: Guid.NewGuid(),
            ProductId: Guid.NewGuid(),
            AgentUserId: Guid.NewGuid(),
            Price: price,
            Quantity: quantity,
            ExpectedVersion: expectedVersion);

    // ── At-least-one-field guard ──────────────────────────────────────────────

    [Fact]
    public async Task Validate_NeitherPriceNorQuantity_FailsValidation()
    {
        // Arrange
        var cmd = Valid(price: null, quantity: null);

        // Act
        var result = await _sut.ValidateAsync(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.ErrorCode == "AtLeastOneFieldRequired");
    }

    // ── Price-only paths ──────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_PriceOnly_Valid_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(price: 125_000m, quantity: null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public async Task Validate_PriceZeroOrNegative_FailsValidation(decimal price)
    {
        var cmd = Valid(price: price, quantity: null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.Price));
    }

    [Fact]
    public async Task Validate_PriceExceedsMaxThreshold_FailsValidation()
    {
        var cmd = Valid(price: MaxPrice + 1m, quantity: null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.Price));
    }

    [Fact]
    public async Task Validate_PriceExceedsMaxThreshold_AtExactMax_Passes()
    {
        var result = await _sut.ValidateAsync(Valid(price: MaxPrice, quantity: null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PriceWithMoreThanTwoDecimals_FailsValidation()
    {
        // 100.123 has 3 decimal places
        var cmd = Valid(price: 100.123m, quantity: null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.Price));
    }

    [Theory]
    [InlineData("125000")]
    [InlineData("125000.5")]
    [InlineData("125000.50")]
    public async Task Validate_PriceWithAtMostTwoDecimals_PassesValidation(string priceStr)
    {
        var price = decimal.Parse(priceStr, System.Globalization.CultureInfo.InvariantCulture);
        var result = await _sut.ValidateAsync(Valid(price: price, quantity: null));
        result.IsValid.Should().BeTrue();
    }

    // ── Quantity-only paths ───────────────────────────────────────────────────

    [Fact]
    public async Task Validate_QuantityOnly_Valid_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(price: null, quantity: 500));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_QuantityZero_PassesValidation()
    {
        // quantity=0 is valid (out-of-stock)
        var result = await _sut.ValidateAsync(Valid(price: null, quantity: 0));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Validate_QuantityNegative_FailsValidation(int quantity)
    {
        var cmd = Valid(price: null, quantity: quantity);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.Quantity));
    }

    // ── Both fields ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_BothPriceAndQuantity_Valid_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(price: 135_000m, quantity: 420));
        result.IsValid.Should().BeTrue();
    }

    // ── Path-param / identity guards ──────────────────────────────────────────

    [Fact]
    public async Task Validate_EmptyMarketId_FailsValidation()
    {
        var cmd = new UpdateProductPriceCommand(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 100m, null, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.MarketId));
    }

    [Fact]
    public async Task Validate_EmptyProductId_FailsValidation()
    {
        var cmd = new UpdateProductPriceCommand(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 100m, null, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.ProductId));
    }

    [Fact]
    public async Task Validate_EmptyAgentUserId_FailsValidation()
    {
        var cmd = new UpdateProductPriceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 100m, null, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateProductPriceCommand.AgentUserId));
    }

    // ── Optional ExpectedVersion ──────────────────────────────────────────────

    [Fact]
    public async Task Validate_WithOptionalExpectedVersion_PassesValidation()
    {
        var result = await _sut.ValidateAsync(
            Valid(price: 100m, expectedVersion: DateTime.UtcNow));
        result.IsValid.Should().BeTrue();
    }
}

using FluentAssertions;
using FreshFlow.Pricing.Application.Commands.CreateMarketProduct;
using FreshFlow.Pricing.Application.Options;
using Microsoft.Extensions.Options;

namespace FreshFlow.Pricing.UnitTests.Commands;

/// <summary>
/// Validates only the STRUCTURAL layer (400 Bad Request) of CreateMarketProductCommand.
///
/// Responsibility split:
/// • 400 (this validator)  — missing IDs, price &gt; max threshold,
///                           price with more than 2 decimal places.
/// • 422 (handler)         — price &lt;= 0 (INVALID_PRICE), quantity &lt; 0 (INVALID_QUANTITY).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreateMarketProductCommandValidatorTests
{
    private const decimal MaxPrice = 10_000_000m; // test threshold

    private static readonly IOptions<PricingOptions> Opts =
        Options.Create(new PricingOptions { MaxPriceVnd = MaxPrice });

    private readonly CreateMarketProductCommandValidator _sut = new(Opts);

    private static CreateMarketProductCommand Valid(
        decimal initialPrice = 25_000m,
        int initialQuantity = 100,
        Guid? createdBy = null) =>
        new(
            MarketId: Guid.NewGuid(),
            ProductId: Guid.NewGuid(),
            InitialPrice: initialPrice,
            InitialQuantity: initialQuantity,
            CreatedBy: createdBy ?? Guid.NewGuid());

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidCommand_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid());
        result.IsValid.Should().BeTrue();
    }

    // ── Price structural rules ──────────────────────────────────────────────

    /// <summary>
    /// price &lt;= 0 is a BUSINESS RULE (422 INVALID_PRICE), NOT a structural constraint.
    /// The validator intentionally lets these through; the handler returns 422.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public async Task Validate_PriceZeroOrNegative_PassesValidator_BusinessRuleEnforcedByHandler(decimal price)
    {
        var cmd = Valid(initialPrice: price);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeTrue(
            "price <= 0 is a 422 business-rule enforced in the handler, not a 400 structural error");
    }

    [Fact]
    public async Task Validate_PriceExceedsMaxThreshold_FailsValidation()
    {
        var cmd = Valid(initialPrice: MaxPrice + 1m);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateMarketProductCommand.InitialPrice));
    }

    [Fact]
    public async Task Validate_PriceExceedsMaxThreshold_AtExactMax_Passes()
    {
        var result = await _sut.ValidateAsync(Valid(initialPrice: MaxPrice));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PriceWithMoreThanTwoDecimals_FailsValidation()
    {
        var cmd = Valid(initialPrice: 100.123m);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateMarketProductCommand.InitialPrice));
    }

    [Theory]
    [InlineData("25000")]
    [InlineData("25000.5")]
    [InlineData("25000.50")]
    public async Task Validate_PriceWithAtMostTwoDecimals_PassesValidation(string priceStr)
    {
        var price = decimal.Parse(priceStr, System.Globalization.CultureInfo.InvariantCulture);
        var result = await _sut.ValidateAsync(Valid(initialPrice: price));
        result.IsValid.Should().BeTrue();
    }

    // ── Quantity structural rules ─────────────────────────────────────────────

    [Fact]
    public async Task Validate_QuantityZero_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(initialQuantity: 0));
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// quantity &lt; 0 is a BUSINESS RULE (422 INVALID_QUANTITY), NOT a structural constraint.
    /// The validator intentionally lets these through; the handler returns 422.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Validate_QuantityNegative_PassesValidator_BusinessRuleEnforcedByHandler(int quantity)
    {
        var cmd = Valid(initialQuantity: quantity);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeTrue(
            "quantity < 0 is a 422 business-rule enforced in the handler, not a 400 structural error");
    }

    // ── Path-param / identity guards ──────────────────────────────────────────

    [Fact]
    public async Task Validate_EmptyMarketId_FailsValidation()
    {
        var cmd = new CreateMarketProductCommand(
            Guid.Empty, Guid.NewGuid(), 25_000m, 100, Guid.NewGuid());
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateMarketProductCommand.MarketId));
    }

    [Fact]
    public async Task Validate_EmptyProductId_FailsValidation()
    {
        var cmd = new CreateMarketProductCommand(
            Guid.NewGuid(), Guid.Empty, 25_000m, 100, Guid.NewGuid());
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateMarketProductCommand.ProductId));
    }

    [Fact]
    public async Task Validate_EmptyCreatedBy_FailsValidation()
    {
        var cmd = new CreateMarketProductCommand(
            Guid.NewGuid(), Guid.NewGuid(), 25_000m, 100, Guid.Empty);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateMarketProductCommand.CreatedBy));
    }
}

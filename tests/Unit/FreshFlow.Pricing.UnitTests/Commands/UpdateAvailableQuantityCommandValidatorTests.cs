using FluentAssertions;
using FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;

namespace FreshFlow.Pricing.UnitTests.Commands;

/// <summary>
/// Validates structural constraints (400 Bad Request) of UpdateAvailableQuantityCommand.
///
/// Responsibility split:
/// • 400 (this validator)  — missing IDs.
/// • 422 (handler)         — quantity &lt; 0 → INVALID_QUANTITY (business rule).
/// • Non-integer body      — rejected at model-binding layer (400).
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpdateAvailableQuantityCommandValidatorTests
{
    private readonly UpdateAvailableQuantityCommandValidator _sut = new();

    private static UpdateAvailableQuantityCommand Valid(
        int quantity = 100,
        DateTime? expectedVersion = null) =>
        new(
            MarketId: Guid.NewGuid(),
            ProductId: Guid.NewGuid(),
            AgentUserId: Guid.NewGuid(),
            Quantity: quantity,
            ExpectedVersion: expectedVersion);

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidCommand_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(quantity: 100));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ZeroQuantity_PassesValidation()
    {
        // quantity=0 is valid → OUT_OF_STOCK
        var result = await _sut.ValidateAsync(Valid(quantity: 0));
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// quantity &lt; 0 is a BUSINESS RULE (422 INVALID_QUANTITY), not a structural constraint.
    /// The validator intentionally allows it; the handler returns 422.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Validate_NegativeQuantity_PassesValidator_BusinessRuleEnforcedByHandler(int quantity)
    {
        var cmd = Valid(quantity: quantity);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeTrue(
            "quantity < 0 is a 422 business-rule enforced in the handler, not a 400 structural error");
    }

    [Fact]
    public async Task Validate_WithOptionalExpectedVersion_PassesValidation()
    {
        var result = await _sut.ValidateAsync(Valid(quantity: 50, expectedVersion: DateTime.UtcNow));
        result.IsValid.Should().BeTrue();
    }

    // ── Identity guards ───────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_EmptyMarketId_FailsValidation()
    {
        var cmd = new UpdateAvailableQuantityCommand(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 100, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateAvailableQuantityCommand.MarketId));
    }

    [Fact]
    public async Task Validate_EmptyProductId_FailsValidation()
    {
        var cmd = new UpdateAvailableQuantityCommand(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 100, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateAvailableQuantityCommand.ProductId));
    }

    [Fact]
    public async Task Validate_EmptyAgentUserId_FailsValidation()
    {
        var cmd = new UpdateAvailableQuantityCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 100, null);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateAvailableQuantityCommand.AgentUserId));
    }
}

using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Safety;

namespace FreshFlow.Assistant.UnitTests.Safety;

[Trait("Category", "Unit")]
public sealed class ConfirmationGateTests
{
    private static readonly ConfirmationGate Gate = new();

    private static string ArgsFor(Guid orderId) =>
        JsonSerializer.Serialize(new { orderId });

    [Fact]
    public void Evaluate_returns_NotApplicable_for_any_tool_other_than_confirm_order()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act — a matching flag must NOT turn a non-confirm tool into an Allow.
        var result = Gate.Evaluate("create_draft_order", ArgsFor(orderId), confirmOrderIdFlag: orderId);

        // Assert
        result.Decision.Should().Be(ConfirmationDecision.NotApplicable);
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_blocks_confirm_order_when_no_confirmation_flag_is_supplied()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act — the LLM asked to confirm but the client sent no confirmOrderId flag.
        var result = Gate.Evaluate(ConfirmationGate.ConfirmOrderToolName, ArgsFor(orderId), confirmOrderIdFlag: null);

        // Assert — blocked, and the pending order id is surfaced so the orchestrator can preview it.
        result.Decision.Should().Be(ConfirmationDecision.Blocked);
        result.OrderId.Should().Be(orderId);
    }

    [Fact]
    public void Evaluate_blocks_confirm_order_when_the_flag_does_not_match_the_order_id()
    {
        // Arrange — user confirmed a different order than the one the LLM is now trying to confirm.
        var orderId = Guid.NewGuid();
        var differentOrderId = Guid.NewGuid();

        // Act
        var result = Gate.Evaluate(
            ConfirmationGate.ConfirmOrderToolName, ArgsFor(orderId), confirmOrderIdFlag: differentOrderId);

        // Assert
        result.Decision.Should().Be(ConfirmationDecision.Blocked);
        result.OrderId.Should().Be(orderId);
    }

    [Fact]
    public void Evaluate_allows_confirm_order_when_the_flag_matches_the_order_id()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act
        var result = Gate.Evaluate(
            ConfirmationGate.ConfirmOrderToolName, ArgsFor(orderId), confirmOrderIdFlag: orderId);

        // Assert
        result.Decision.Should().Be(ConfirmationDecision.Allowed);
        result.IsBlocked.Should().BeFalse();
        result.OrderId.Should().Be(orderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"orderId\":\"not-a-guid\"}")]
    [InlineData("[]")]
    public void Evaluate_blocks_confirm_order_when_the_order_id_cannot_be_parsed(string? malformedArgs)
    {
        // Arrange — a flag is present, but the LLM args carry no usable orderId to match against.
        var flag = Guid.NewGuid();

        // Act
        var result = Gate.Evaluate(ConfirmationGate.ConfirmOrderToolName, malformedArgs, confirmOrderIdFlag: flag);

        // Assert — never allow a confirm we cannot pin to a concrete order id.
        result.Decision.Should().Be(ConfirmationDecision.Blocked);
        result.OrderId.Should().BeNull();
    }
}

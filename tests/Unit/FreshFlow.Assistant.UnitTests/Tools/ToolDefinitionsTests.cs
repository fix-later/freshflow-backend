using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Tools;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;
using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;
using FreshFlow.SharedKernel.Application;
using MediatR;
using NSubstitute;

namespace FreshFlow.Assistant.UnitTests.Tools;

/// <summary>
/// Covers T2 acceptance: each MVP tool maps LLM args to the right MediatR record with server-side
/// UserId/MarketId injection (never LLM-supplied), maps Result&lt;T&gt; success/failure to JSON
/// without throwing, and rejects malformed args with a structured error.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ToolDefinitionsTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _marketId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _deliveryAddressId = Guid.NewGuid();

    private AssistantToolInvocationContext Ctx => new(_userId, _marketId);

    private IReadOnlyDictionary<string, AssistantTool> Tools =>
        ToolDefinitions.CreateAll(_sender).ToDictionary(t => t.Name);

    // ── search_products ─────────────────────────────────────────────────────

    [Fact]
    public async Task search_products_injects_MarketId_from_context_and_ignores_any_LLM_supplied_value()
    {
        // Arrange
        var spoofedMarketId = Guid.NewGuid();
        var args = JsonSerializer.SerializeToElement(new { searchText = "cà chua", marketId = spoofedMarketId });
        _sender.Send(Arg.Any<SearchMarketProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<SearchMarketProductsResultDto>.Success(new SearchMarketProductsResultDto([], null)));

        // Act
        await Tools["search_products"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        await _sender.Received(1).Send(
            Arg.Is<SearchMarketProductsQuery>(q => q.MarketId == _marketId && q.SearchText == "cà chua"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task search_products_returns_a_structured_error_when_no_market_is_in_context()
    {
        // Arrange
        var ctxWithoutMarket = new AssistantToolInvocationContext(_userId, MarketId: null);
        var args = JsonSerializer.SerializeToElement(new { searchText = "cà chua" });

        // Act
        var result = await Tools["search_products"].Handler!(args, ctxWithoutMarket, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("MARKET_CONTEXT_MISSING");
        await _sender.DidNotReceive().Send(Arg.Any<SearchMarketProductsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task search_products_returns_a_structured_error_for_malformed_args()
    {
        // Arrange — "searchText" must be a string; the LLM sent a number instead.
        var badArgs = JsonSerializer.SerializeToElement(new { searchText = 123 });

        // Act
        var result = await Tools["search_products"].Handler!(badArgs, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("INVALID_TOOL_ARGS");
        await _sender.DidNotReceive().Send(Arg.Any<SearchMarketProductsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task search_products_returns_a_structured_error_when_searchText_is_missing()
    {
        // Arrange — System.Text.Json does not enforce required-ness; a missing field must not
        // silently dispatch with an empty/default value.
        var args = JsonSerializer.SerializeToElement(new { category = "rau" });

        // Act
        var result = await Tools["search_products"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("INVALID_TOOL_ARGS");
        await _sender.DidNotReceive().Send(Arg.Any<SearchMarketProductsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task search_products_maps_a_failure_result_to_a_structured_error_without_throwing()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { searchText = "cà chua" });
        _sender.Send(Arg.Any<SearchMarketProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<SearchMarketProductsResultDto>.Failure(new Error("MARKET_NOT_FOUND", "Market not found.")));

        // Act
        var result = await Tools["search_products"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("MARKET_NOT_FOUND");
    }

    // ── create_draft_order ──────────────────────────────────────────────────

    [Fact]
    public async Task create_draft_order_injects_UserId_from_context_and_ignores_any_LLM_supplied_value()
    {
        // Arrange
        var spoofedUserId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        var args = JsonSerializer.SerializeToElement(new
        {
            userId = spoofedUserId,
            items = new[] { new { marketProductId, quantity = 3 } }
        });
        _sender.Send(Arg.Any<CreateDraftOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderDto>.Success(SampleOrder()));

        // Act
        await Tools["create_draft_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        await _sender.Received(1).Send(
            Arg.Is<CreateDraftOrderCommand>(c =>
                c.UserId == _userId &&
                c.Items.Count == 1 &&
                c.Items[0].MarketProductId == marketProductId &&
                c.Items[0].Quantity == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task create_draft_order_returns_a_structured_error_when_items_is_missing()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { notes = "giao trước 9h" });

        // Act
        var result = await Tools["create_draft_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("INVALID_TOOL_ARGS");
        await _sender.DidNotReceive().Send(Arg.Any<CreateDraftOrderCommand>(), Arg.Any<CancellationToken>());
    }

    // ── get_order ────────────────────────────────────────────────────────────

    [Fact]
    public async Task get_order_injects_UserId_and_forces_IsAdmin_false_regardless_of_LLM_input()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId, isAdmin = true });
        _sender.Send(Arg.Any<GetOrderQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderDto>.Success(SampleOrder()));

        // Act
        await Tools["get_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        await _sender.Received(1).Send(
            Arg.Is<GetOrderQuery>(q => q.UserId == _userId && q.OrderId == _orderId && !q.IsAdmin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task get_order_returns_a_structured_error_when_orderId_is_missing()
    {
        // Arrange — shared OrderId-args parsing used by get_order/preview_confirmation/confirm_order.
        var args = JsonSerializer.SerializeToElement(new { });

        // Act
        var result = await Tools["get_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("INVALID_TOOL_ARGS");
        await _sender.DidNotReceive().Send(Arg.Any<GetOrderQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task get_order_maps_a_not_found_failure_to_a_structured_error()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId });
        _sender.Send(Arg.Any<GetOrderQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderDto>.Failure(Error.NotFound("Order", _orderId)));

        // Act
        var result = await Tools["get_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("ORDER_NOT_FOUND");
    }

    // ── preview_confirmation ─────────────────────────────────────────────────

    [Fact]
    public async Task preview_confirmation_injects_UserId_and_dispatches_the_right_order()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId });
        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Success(
                new OrderConfirmationPreviewDto(true, [], 100_000m, null, 5_000_000m)));

        // Act
        await Tools["preview_confirmation"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        await _sender.Received(1).Send(
            Arg.Is<PreviewOrderConfirmationQuery>(q => q.UserId == _userId && q.OrderId == _orderId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task preview_confirmation_strips_RemainingCreditAfter_from_the_payload_sent_to_the_llm()
    {
        // Arrange — design doc §2: credit balance never enters the LLM prompt.
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId });
        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Success(
                new OrderConfirmationPreviewDto(true, [], 100_000m, null, 5_000_000m)));

        // Act
        var result = await Tools["preview_confirmation"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        result.Should().NotContain("5000000").And.NotContain("remainingCreditAfter");
        var parsed = JsonDocument.Parse(result).RootElement;
        parsed.GetProperty("wouldSucceed").GetBoolean().Should().BeTrue();
        parsed.GetProperty("totalAmount").GetDecimal().Should().Be(100_000m);
    }

    [Fact]
    public async Task preview_confirmation_maps_a_failure_result_without_leaking_remaining_credit()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId });
        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Failure(Error.NotFound("Order", _orderId)));

        // Act
        var result = await Tools["preview_confirmation"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("ORDER_NOT_FOUND");
    }

    // ── confirm_order ────────────────────────────────────────────────────────

    [Fact]
    public void confirm_order_schema_requires_deliveryAddressId_without_changing_get_order()
    {
        var confirmSchema = Tools["confirm_order"].ParametersSchema;
        var getOrderSchema = Tools["get_order"].ParametersSchema;

        confirmSchema.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("deliveryAddressId");
        getOrderSchema.GetProperty("properties").TryGetProperty(
            "deliveryAddressId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task confirm_order_injects_UserId_from_context_and_ignores_any_LLM_supplied_value()
    {
        // Arrange
        var spoofedUserId = Guid.NewGuid();
        var args = JsonSerializer.SerializeToElement(new
        {
            orderId = _orderId,
            deliveryAddressId = _deliveryAddressId,
            userId = spoofedUserId
        });
        _sender.Send(Arg.Any<ConfirmOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderDto>.Success(SampleOrder()));

        // Act
        await Tools["confirm_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        await _sender.Received(1).Send(
            Arg.Is<ConfirmOrderCommand>(c =>
                c.UserId == _userId &&
                c.OrderId == _orderId &&
                c.DeliveryAddressId == _deliveryAddressId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task confirm_order_maps_a_credit_limit_failure_to_a_structured_error_the_llm_can_explain()
    {
        // Arrange
        var args = JsonSerializer.SerializeToElement(
            new { orderId = _orderId, deliveryAddressId = _deliveryAddressId });
        _sender.Send(Arg.Any<ConfirmOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderDto>.Failure(new Error("CREDIT_LIMIT_EXCEEDED", "Vượt hạn mức công nợ.")));

        // Act
        var result = await Tools["confirm_order"].Handler!(args, Ctx, CancellationToken.None);

        // Assert
        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString().Should().Be("CREDIT_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task confirm_order_rejects_missing_deliveryAddressId()
    {
        var args = JsonSerializer.SerializeToElement(new { orderId = _orderId });

        var result = await Tools["confirm_order"].Handler!(args, Ctx, CancellationToken.None);

        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString()
            .Should().Be("INVALID_TOOL_ARGS");
        await _sender.DidNotReceive().Send(
            Arg.Any<ConfirmOrderCommand>(), Arg.Any<CancellationToken>());
    }

    private static OrderDto SampleOrder() => new(
        OrderId: Guid.NewGuid(),
        RestaurantId: Guid.NewGuid(),
        Status: "Draft",
        PaymentStatus: "Pending",
        ScheduledFor: null,
        TotalAmount: 0m,
        Notes: null,
        DeliveryAddress: null,
        Items: [],
        OrderGroupId: null,
        ScheduledOrderId: null,
        CancelledAt: null,
        CancellationReason: null,
        ConfirmedReceiptAt: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);
}

using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant;
using FreshFlow.API.Assistant.Tools;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetDeliveryAddresses;
using FreshFlow.Auth.Application.Queries.GetRestaurantProfile;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Commands.Favorites.Add;
using FreshFlow.Orders.Application.Commands.Favorites.Remove;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.GetFavorites;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Application.Queries.GetRestaurantCredit;
using FreshFlow.Orders.Application.Queries.ListOrders;
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

    private AssistantToolInvocationContext Ctx => new(_userId, _marketId, _deliveryAddressId);

    private IReadOnlyDictionary<string, AssistantTool> Tools =>
        ToolDefinitions.CreateAll(_sender).ToDictionary(t => t.Name);

    [Fact]
    public void CreateAll_registers_the_original_and_six_P0_tools_once()
    {
        Tools.Keys.Should().BeEquivalentTo(
            "search_products",
            "list_favorites",
            "add_favorite",
            "remove_favorite",
            "get_my_credit",
            "list_my_orders",
            "list_delivery_addresses",
            "create_draft_order",
            "get_order",
            "preview_confirmation",
            "confirm_order");
        Tools.Should().HaveCount(11);
    }

    [Fact]
    public void Packing_quantity_rule_is_exposed_to_the_LLM()
    {
        AssistantSystemPrompt.Text.Should().Contain("sellingUnit.weightKg").And.Contain("bội số");
        Tools["search_products"].Description.Should().Contain("sellingUnit.weightKg");

        var createDraft = Tools["create_draft_order"];
        createDraft.Description.Should().Contain("sellingUnit.weightKg").And.Contain("bội số");
        createDraft.ParametersSchema
            .GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("quantity")
            .GetProperty("description")
            .GetString()
            .Should().Contain("sellingUnit.weightKg").And.Contain("bội số");
    }

    [Fact]
    public async Task list_favorites_injects_UserId_and_caps_the_LLM_payload()
    {
        var favorites = Enumerable.Range(1, 21)
            .Select(i => new FavoriteItemDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Sản phẩm {i}",
                null,
                _marketId,
                "Chợ",
                "Rau",
                "kg",
                10_000m,
                5,
                DateTime.UtcNow))
            .ToList();
        _sender.Send(Arg.Any<GetFavoritesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<FavoriteItemDto>>.Success(favorites));

        var result = await Tools["list_favorites"].Handler!(
            JsonSerializer.SerializeToElement(new { userId = Guid.NewGuid() }), Ctx, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<GetFavoritesQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
        var payload = JsonDocument.Parse(result).RootElement;
        payload.GetProperty("items").GetArrayLength().Should().Be(20);
        payload.GetProperty("totalCount").GetInt32().Should().Be(21);
        payload.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task add_and_remove_favorite_inject_UserId_and_ignore_spoofed_identity()
    {
        var marketProductId = Guid.NewGuid();
        var args = JsonSerializer.SerializeToElement(new
        {
            marketProductId,
            userId = Guid.NewGuid(),
            restaurantId = Guid.NewGuid()
        });
        _sender.Send(Arg.Any<AddFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddFavoriteResponse>.Success(new AddFavoriteResponse(marketProductId)));
        _sender.Send(Arg.Any<RemoveFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await Tools["add_favorite"].Handler!(args, Ctx, CancellationToken.None);
        var removeResult = await Tools["remove_favorite"].Handler!(args, Ctx, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<AddFavoriteCommand>(c => c.UserId == _userId && c.MarketProductId == marketProductId),
            Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(
            Arg.Is<RemoveFavoriteCommand>(c => c.UserId == _userId && c.MarketProductId == marketProductId),
            Arg.Any<CancellationToken>());
        JsonDocument.Parse(removeResult).RootElement.GetProperty("removed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task get_my_credit_resolves_the_authenticated_restaurant_and_never_accepts_an_admin_flag()
    {
        var restaurantId = Guid.NewGuid();
        _sender.Send(Arg.Any<GetRestaurantProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<GetRestaurantProfileResponse>.Success(SampleProfile(restaurantId)));
        _sender.Send(Arg.Any<GetRestaurantCreditQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Success(
                new RestaurantCreditDto(restaurantId, 1_000_000m, 250_000m, 750_000m, DateTime.UtcNow)));

        await Tools["get_my_credit"].Handler!(
            JsonSerializer.SerializeToElement(new
            {
                restaurantId = Guid.NewGuid(),
                userId = Guid.NewGuid(),
                isAdmin = true
            }),
            Ctx,
            CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<GetRestaurantProfileQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(
            Arg.Is<GetRestaurantCreditQuery>(q =>
                q.UserId == _userId && q.RestaurantId == restaurantId && !q.IsAdmin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task list_my_orders_forces_owner_scope_and_validates_page_size()
    {
        _sender.Send(Arg.Any<ListOrdersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderListResponseDto>.Success(
                new OrderListResponseDto([], new OrderPaginationMeta(0, 2, 5))));
        var args = JsonSerializer.SerializeToElement(new
        {
            status = "delivered",
            page = 2,
            pageSize = 5,
            userId = Guid.NewGuid(),
            restaurantId = Guid.NewGuid(),
            isAdmin = true
        });

        await Tools["list_my_orders"].Handler!(args, Ctx, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<ListOrdersQuery>(q =>
                q.UserId == _userId
                && !q.IsAdmin
                && q.RestaurantId == null
                && q.Status == "delivered"
                && q.Page == 2
                && q.PageSize == 5),
            Arg.Any<CancellationToken>());

        var invalid = await Tools["list_my_orders"].Handler!(
            JsonSerializer.SerializeToElement(new { pageSize = 21 }), Ctx, CancellationToken.None);
        JsonDocument.Parse(invalid).RootElement.GetProperty("error").GetString().Should().Be("INVALID_TOOL_ARGS");
        await _sender.Received(1).Send(Arg.Any<ListOrdersQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task list_delivery_addresses_injects_UserId()
    {
        var address = new DeliveryAddressDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bếp trưởng",
            "0900000000",
            "123 Nguyễn Huệ",
            null,
            null,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow);
        _sender.Send(Arg.Any<GetDeliveryAddressesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DeliveryAddressDto>>.Success([address]));

        await Tools["list_delivery_addresses"].Handler!(
            JsonSerializer.SerializeToElement(new { userId = Guid.NewGuid() }), Ctx, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<GetDeliveryAddressesQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

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
    public void confirm_order_schema_keeps_deliveryAddressId_out_of_llm_control()
    {
        var confirmSchema = Tools["confirm_order"].ParametersSchema;
        var getOrderSchema = Tools["get_order"].ParametersSchema;

        confirmSchema.GetProperty("properties").TryGetProperty(
            "deliveryAddressId", out _).Should().BeFalse();
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
        var ctx = new AssistantToolInvocationContext(_userId, _marketId);

        var result = await Tools["confirm_order"].Handler!(args, ctx, CancellationToken.None);

        JsonDocument.Parse(result).RootElement.GetProperty("error").GetString()
            .Should().Be("DELIVERY_ADDRESS_REQUIRED");
        await _sender.DidNotReceive().Send(
            Arg.Any<ConfirmOrderCommand>(), Arg.Any<CancellationToken>());
    }

    private static GetRestaurantProfileResponse SampleProfile(Guid restaurantId) => new(
        restaurantId,
        "Nhà hàng",
        "approved",
        null,
        null,
        null,
        null,
        DateTime.UtcNow);

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

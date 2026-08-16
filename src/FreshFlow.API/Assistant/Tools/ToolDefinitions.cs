using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;
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
using MediatR;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Declares the assistant tools exposed to the LLM. Each tool is a thin wrapper over existing
/// MediatR commands/queries: parse LLM args → inject server-side identity (never LLM-supplied) →
/// <see cref="ISender.Send"/> → map the result to JSON. Sensitive tool results are stripped by the
/// orchestrator before conversation history is sent back to the provider.
/// </summary>
public static class ToolDefinitions
{
    private const int MaxFavoriteItems = 20;

    /// <summary>Builds the fixed tool set, bound to the given <see cref="ISender"/>.</summary>
    public static IReadOnlyList<AssistantTool> CreateAll(ISender sender) =>
    [
        SearchProducts(sender),
        ListFavorites(sender),
        AddFavorite(sender),
        RemoveFavorite(sender),
        GetMyCredit(sender),
        ListMyOrders(sender),
        ListDeliveryAddresses(sender),
        CreateDraftOrder(sender),
        GetOrder(sender),
        PreviewConfirmation(sender),
        ConfirmOrder(sender)
    ];

    private static AssistantTool SearchProducts(ISender sender) => new(
        Name: "search_products",
        Description: "Tìm sản phẩm trong chợ theo tên hoặc danh mục. Trả về giá, số lượng còn lại và sellingUnit.weightKg dùng làm bước số lượng đặt.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                searchText = new { type = "string", description = "Tên hoặc từ khóa sản phẩm cần tìm." },
                category = new { type = "string", description = "Lọc theo danh mục (tùy chọn)." },
                inStockOnly = new { type = "boolean", description = "Chỉ lấy sản phẩm còn hàng." },
                cursor = new { type = "string", description = "Con trỏ phân trang của lần tìm trước (tùy chọn)." }
            },
            required: ["searchText"]),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<SearchProductsArgs>(argsJson, out var args, out var error))
            {
                return error!;
            }

            if (string.IsNullOrWhiteSpace(args!.SearchText))
            {
                return ToolResultJson.Error("INVALID_TOOL_ARGS", "Tool arguments must include a non-empty searchText.");
            }

            if (ctx.MarketId is null)
            {
                return ToolResultJson.Error("MARKET_CONTEXT_MISSING", "No active market selected for this session.");
            }

            var query = new SearchMarketProductsQuery(
                MarketId: ctx.MarketId.Value,
                SearchText: args.SearchText,
                Category: args.Category,
                InStockOnly: args.InStockOnly,
                Cursor: args.Cursor);

            var result = await sender.Send(query, ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool ListFavorites(ISender sender) => new(
        Name: "list_favorites",
        Description: "Xem danh sách sản phẩm yêu thích của nhà hàng.",
        ParametersSchema: ToolArgsSchema.Object(new { }, []),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<EmptyArgs>(argsJson, out _, out var error))
            {
                return error!;
            }

            var result = await sender.Send(new GetFavoritesQuery(ctx.UserId), ct);
            if (result.IsFailure)
            {
                return ToolResultJson.Error(result.Error.Code, result.Error.Message);
            }

            var items = result.Value.Take(MaxFavoriteItems).ToList();
            return ToolResultJson.Success(new
            {
                items,
                totalCount = result.Value.Count,
                truncated = result.Value.Count > items.Count
            });
        });

    private static AssistantTool AddFavorite(ISender sender) => new(
        Name: "add_favorite",
        Description: "Thêm một sản phẩm trong chợ vào danh sách yêu thích khi người dùng yêu cầu rõ ràng.",
        ParametersSchema: FavoriteArgsSchema(),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!TryParseMarketProductId(argsJson, out var marketProductId, out var error))
            {
                return error!;
            }

            var result = await sender.Send(new AddFavoriteCommand(ctx.UserId, marketProductId), ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool RemoveFavorite(ISender sender) => new(
        Name: "remove_favorite",
        Description: "Bỏ một sản phẩm khỏi danh sách yêu thích khi người dùng yêu cầu rõ ràng.",
        ParametersSchema: FavoriteArgsSchema(),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!TryParseMarketProductId(argsJson, out var marketProductId, out var error))
            {
                return error!;
            }

            var result = await sender.Send(new RemoveFavoriteCommand(ctx.UserId, marketProductId), ct);
            return result.IsSuccess
                ? ToolResultJson.Success(new { marketProductId, removed = true })
                : ToolResultJson.Error(result.Error.Code, result.Error.Message);
        });

    private static AssistantTool GetMyCredit(ISender sender) => new(
        Name: "get_my_credit",
        Description: "Xem hạn mức, dư nợ và công nợ còn lại của nhà hàng hiện tại.",
        ParametersSchema: ToolArgsSchema.Object(new { }, []),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<EmptyArgs>(argsJson, out _, out var error))
            {
                return error!;
            }

            var profile = await sender.Send(new GetRestaurantProfileQuery(ctx.UserId), ct);
            if (profile.IsFailure)
            {
                return ToolResultJson.Error(profile.Error.Code, profile.Error.Message);
            }

            var result = await sender.Send(
                new GetRestaurantCreditQuery(ctx.UserId, IsAdmin: false, profile.Value.RestaurantId), ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool ListMyOrders(ISender sender) => new(
        Name: "list_my_orders",
        Description: "Xem các đơn hàng của nhà hàng hiện tại, có thể lọc theo trạng thái và thời gian.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                status = new
                {
                    type = "string",
                    description = "draft, confirmed, batched, picked_up, at_hub, delivering, delivered hoặc cancelled."
                },
                from = new { type = "string", description = "Thời điểm bắt đầu theo ISO 8601 (tùy chọn)." },
                to = new { type = "string", description = "Thời điểm kết thúc theo ISO 8601 (tùy chọn)." },
                page = new { type = "integer", description = "Trang cần xem, mặc định 1." },
                pageSize = new { type = "integer", description = "Số đơn mỗi trang, mặc định 10 và tối đa 20." }
            },
            required: []),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<ListMyOrdersArgs>(argsJson, out var args, out var error))
            {
                return error!;
            }

            if (args!.Page < 1 || args.PageSize is < 1 or > 20)
            {
                return ToolResultJson.Error(
                    "INVALID_TOOL_ARGS", "page must be positive and pageSize must be between 1 and 20.");
            }

            var query = new ListOrdersQuery(
                UserId: ctx.UserId,
                IsAdmin: false,
                RestaurantId: null,
                Status: args.Status,
                From: args.From,
                To: args.To,
                Sort: "createdAt:desc",
                Page: args.Page,
                PageSize: args.PageSize);

            var result = await sender.Send(query, ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool ListDeliveryAddresses(ISender sender) => new(
        Name: "list_delivery_addresses",
        Description: "Xem các địa chỉ giao hàng đang hoạt động của nhà hàng hiện tại.",
        ParametersSchema: ToolArgsSchema.Object(new { }, []),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<EmptyArgs>(argsJson, out _, out var error))
            {
                return error!;
            }

            var result = await sender.Send(new GetDeliveryAddressesQuery(ctx.UserId), ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool CreateDraftOrder(ISender sender) => new(
        Name: "create_draft_order",
        Description: "Tạo đơn hàng nháp với số lượng người dùng đã xác nhận; nếu search_products trả sellingUnit.weightKg thì quantity phải là bội số của giá trị đó.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                items = new
                {
                    type = "array",
                    description = "Danh sách sản phẩm cần thêm vào đơn.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            marketProductId = new { type = "string", description = "Id sản phẩm trong chợ (UUID)." },
                            quantity = new { type = "integer", description = "Tổng số lượng; phải là bội số dương của sellingUnit.weightKg khi sản phẩm có giá trị này." }
                        },
                        required = new[] { "marketProductId", "quantity" }
                    }
                },
                scheduledFor = new { type = "string", description = "Thời gian giao mong muốn (ISO 8601, tùy chọn)." },
                notes = new { type = "string", description = "Ghi chú cho đơn hàng (tùy chọn)." }
            },
            required: ["items"]),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!ToolArgsParser.TryParse<CreateDraftOrderArgs>(argsJson, out var args, out var error))
            {
                return error!;
            }

            if (args!.Items is null or [])
            {
                return ToolResultJson.Error("INVALID_TOOL_ARGS", "Tool arguments must include at least one item.");
            }

            var items = args.Items
                .Select(i => new DraftOrderItemRequest(i.MarketProductId, i.Quantity))
                .ToList();

            var command = new CreateDraftOrderCommand(
                UserId: ctx.UserId,
                Items: items,
                ScheduledFor: args.ScheduledFor,
                Notes: args.Notes);

            var result = await sender.Send(command, ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool GetOrder(ISender sender) => new(
        Name: "get_order",
        Description: "Lấy thông tin chi tiết một đơn hàng theo id.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                orderId = new { type = "string", description = "Id đơn hàng (UUID)." }
            },
            required: ["orderId"]),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!TryParseOrderId(argsJson, out var orderId, out var error))
            {
                return error!;
            }

            // canReadAll is always false in the assistant context — the LLM only ever reads orders
            // belonging to the authenticated user, regardless of that user's admin role elsewhere.
            var query = new GetOrderQuery(UserId: ctx.UserId, IsAdmin: false, OrderId: orderId);

            var result = await sender.Send(query, ct);
            return ToolResultJson.From(result);
        });

    private static AssistantTool PreviewConfirmation(ISender sender) => new(
        Name: "preview_confirmation",
        Description: "Xem trước kết quả xác nhận đơn hàng (kiểm tra hạn mức công nợ, giờ cắt đơn) mà không xác nhận thật.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                orderId = new { type = "string", description = "Id đơn hàng (UUID)." }
            },
            required: ["orderId"]),
        Handler: async (argsJson, ctx, ct) =>
        {
            if (!TryParseOrderId(argsJson, out var orderId, out var error))
            {
                return error!;
            }

            if (ctx.DeliveryAddressId is not { } deliveryAddressId)
            {
                return ToolResultJson.Error(
                    "DELIVERY_ADDRESS_REQUIRED", "The client must select a delivery address.");
            }

            var query = new PreviewOrderConfirmationQuery(
                UserId: ctx.UserId, OrderId: orderId, DeliveryAddressId: deliveryAddressId);
            var result = await sender.Send(query, ct);

            // RemainingCreditAfter never enters the LLM prompt — it goes straight to the client in
            // the orchestrator's response payload, outside conversation history (§2 of the design doc).
            return ToolResultJson.From(result, nameof(OrderConfirmationPreviewDto.RemainingCreditAfter));
        });

    private static AssistantTool ConfirmOrder(ISender sender) => new(
        Name: "confirm_order",
        Description: "Xác nhận đơn hàng nháp thành đơn chính thức (trừ công nợ, khóa giá). Chỉ gọi sau khi người dùng đồng ý rõ ràng.",
        ParametersSchema: ToolArgsSchema.Object(
            properties: new
            {
                orderId = new { type = "string", description = "Id đơn hàng (UUID)." }
            },
            required: ["orderId"]),
        Handler: async (argsJson, ctx, ct) =>
        {
            // The two-phase ConfirmationGate (T4) sits in front of this tool in the orchestrator —
            // it intercepts the "confirm_order" call before the registry ever dispatches here, so
            // this handler can assume the explicit-confirmation invariant already holds.
            if (!ToolArgsParser.TryParse<OrderIdArgs>(argsJson, out var args, out var error))
            {
                return error!;
            }

            if (args!.OrderId is not { } orderId)
            {
                return ToolResultJson.Error(
                    "INVALID_TOOL_ARGS", "Tool arguments must include orderId.");
            }

            if (ctx.DeliveryAddressId is not { } deliveryAddressId)
            {
                return ToolResultJson.Error(
                    "DELIVERY_ADDRESS_REQUIRED", "The client must select a delivery address.");
            }

            var command = new ConfirmOrderCommand(
                UserId: ctx.UserId, OrderId: orderId, DeliveryAddressId: deliveryAddressId);
            var result = await sender.Send(command, ct);
            return ToolResultJson.From(result);
        });

    /// <summary>Shared arg parsing for tools that only take an <c>orderId</c>.</summary>
    private static bool TryParseOrderId(JsonElement argsJson, out Guid orderId, out string? error)
    {
        if (!ToolArgsParser.TryParse<OrderIdArgs>(argsJson, out var args, out error))
        {
            orderId = Guid.Empty;
            return false;
        }

        if (args!.OrderId is not { } parsedOrderId)
        {
            orderId = Guid.Empty;
            error = ToolResultJson.Error("INVALID_TOOL_ARGS", "Tool arguments must include orderId.");
            return false;
        }

        orderId = parsedOrderId;
        return true;
    }

    private static JsonElement FavoriteArgsSchema() =>
        ToolArgsSchema.Object(
            properties: new
            {
                marketProductId = new
                {
                    type = "string",
                    description = "Id sản phẩm trong chợ (UUID) từ kết quả tìm kiếm hoặc danh sách yêu thích."
                }
            },
            required: ["marketProductId"]);

    private static bool TryParseMarketProductId(
        JsonElement argsJson,
        out Guid marketProductId,
        out string? error)
    {
        if (!ToolArgsParser.TryParse<FavoriteArgs>(argsJson, out var args, out error))
        {
            marketProductId = Guid.Empty;
            return false;
        }

        if (args!.MarketProductId is not { } parsedId || parsedId == Guid.Empty)
        {
            marketProductId = Guid.Empty;
            error = ToolResultJson.Error(
                "INVALID_TOOL_ARGS", "Tool arguments must include a valid marketProductId.");
            return false;
        }

        marketProductId = parsedId;
        return true;
    }

    private sealed record SearchProductsArgs(
        string? SearchText = null,
        string? Category = null,
        bool InStockOnly = false,
        string? Cursor = null);

    private sealed record CreateDraftOrderArgs(
        IReadOnlyList<CreateDraftOrderItemArgs>? Items = null,
        DateTime? ScheduledFor = null,
        string? Notes = null);

    private sealed record CreateDraftOrderItemArgs(Guid MarketProductId, int Quantity);

    private sealed record EmptyArgs;

    private sealed record FavoriteArgs(Guid? MarketProductId = null);

    private sealed record ListMyOrdersArgs(
        string? Status = null,
        DateTime? From = null,
        DateTime? To = null,
        int Page = 1,
        int PageSize = 10);

    // Guid? rather than Guid — a missing "orderId" must surface as INVALID_TOOL_ARGS, not silently
    // dispatch with Guid.Empty (System.Text.Json does not enforce required-ness on its own).
    private sealed record OrderIdArgs(Guid? OrderId = null);

}

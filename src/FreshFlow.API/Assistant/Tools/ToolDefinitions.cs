using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;
using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;
using MediatR;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Declares the 5 MVP tools exposed to the LLM (DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md
/// §3). Each tool is a thin 1:1 wrapper over an existing MediatR command/query — handlers contain no
/// business rules, only: parse LLM args → inject server-side identity (UserId/MarketId, never
/// LLM-supplied) → <see cref="ISender.Send"/> → map <c>Result&lt;T&gt;</c> to the JSON the LLM sees.
/// </summary>
public static class ToolDefinitions
{
    /// <summary>Builds the fixed MVP tool set, bound to the given <see cref="ISender"/>.</summary>
    public static IReadOnlyList<AssistantTool> CreateAll(ISender sender) =>
    [
        SearchProducts(sender),
        CreateDraftOrder(sender),
        GetOrder(sender),
        PreviewConfirmation(sender),
        ConfirmOrder(sender)
    ];

    private static AssistantTool SearchProducts(ISender sender) => new(
        Name: "search_products",
        Description: "Tìm sản phẩm trong chợ theo tên hoặc danh mục. Trả về giá hiện tại và số lượng còn lại.",
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

    private static AssistantTool CreateDraftOrder(ISender sender) => new(
        Name: "create_draft_order",
        Description: "Tạo đơn hàng nháp (draft) với danh sách sản phẩm và số lượng đã chọn.",
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
                            quantity = new { type = "integer", description = "Số lượng." }
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

            var query = new PreviewOrderConfirmationQuery(UserId: ctx.UserId, OrderId: orderId);
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

    // Guid? rather than Guid — a missing "orderId" must surface as INVALID_TOOL_ARGS, not silently
    // dispatch with Guid.Empty (System.Text.Json does not enforce required-ness on its own).
    private sealed record OrderIdArgs(Guid? OrderId = null);

}

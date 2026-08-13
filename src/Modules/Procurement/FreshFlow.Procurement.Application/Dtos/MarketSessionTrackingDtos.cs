namespace FreshFlow.Procurement.Application.Dtos;

public sealed record MarketSessionTrackingDto(
    MarketSessionDto Session,
    MarketSessionTrackingSummaryDto Summary,
    IReadOnlyList<MarketSessionTrackingProductDto> Products,
    IReadOnlyList<MarketSessionTrackingOrderDto> Orders,
    ProcurementBatchPaginationDto OrdersPagination,
    MarketSessionTrackingBatchDto? Batch);

public sealed record MarketSessionTrackingSummaryDto(
    int TotalOrders,
    int ActiveOrders,
    int CancelledOrders,
    int TotalLineItems,
    long TotalQuantity,
    decimal MerchandiseAmount = 0m,
    decimal VatAmount = 0m,
    decimal DeliveryFee = 0m,
    decimal GrandTotal = 0m);

public sealed record MarketSessionTrackingProductDto(
    Guid MarketProductId,
    string ProductName,
    int OrderCount,
    long TotalQuantity);

public sealed record MarketSessionTrackingOrderDto(
    Guid OrderId,
    Guid RestaurantId,
    string RestaurantName,
    string Status,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal DeliveryFee,
    decimal TotalAmount,
    DateTime? ConfirmedAt,
    IReadOnlyList<MarketSessionTrackingOrderItemDto> Items);

public sealed record MarketSessionTrackingOrderItemDto(
    Guid MarketProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record MarketSessionTrackingBatchDto(
    Guid Id,
    string? Code,
    string Status);

public sealed record MarketSessionTrackingData(
    MarketSessionTrackingSummaryDto Summary,
    IReadOnlyList<MarketSessionTrackingProductDto> Products,
    IReadOnlyList<MarketSessionTrackingOrderDto> Orders,
    ProcurementBatchPaginationDto OrdersPagination,
    MarketSessionTrackingBatchDto? Batch);

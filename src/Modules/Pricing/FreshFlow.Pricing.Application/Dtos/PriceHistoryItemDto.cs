namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// A single entry in the price/quantity change history for a market product.
/// Returned by GET /api/v1/markets/{marketId}/products/{productId}/price-history (UC-PRI-10).
/// </summary>
public sealed record PriceHistoryItemDto(
    Guid Id,
    Guid MarketProductId,
    decimal Price,
    int Quantity,
    Guid? RecordedBy,
    DateTime RecordedAt);

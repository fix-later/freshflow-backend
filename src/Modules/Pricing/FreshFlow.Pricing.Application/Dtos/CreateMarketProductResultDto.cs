namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Response DTO returned after a product has been listed at a market.
/// Corresponds to POST /api/v1/markets/{marketId}/products response.
/// </summary>
public sealed record CreateMarketProductResultDto(
    Guid MarketProductId,
    Guid MarketId,
    Guid ProductId,
    decimal CurrentPrice,
    int CurrentQuantity,
    DateTime CreatedAt,
    Guid? CreatedBy);

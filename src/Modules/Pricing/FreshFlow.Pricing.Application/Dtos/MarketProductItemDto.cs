namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Single item in the market product list response.
/// Returned by GET /api/v1/markets/{marketId}/products.
/// </summary>
public sealed record MarketProductItemDto(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    string ProductName,
    string? Category,
    string Unit,
    decimal CurrentPrice,
    int CurrentQuantity,
    int AvailableQuantity,
    DateTime UpdatedAt,
    Guid? UpdatedBy);

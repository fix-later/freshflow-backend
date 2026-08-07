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
    IReadOnlyList<MarketProductTagDto> Tags,
    DateTime UpdatedAt,
    Guid? UpdatedBy,
    SellingUnitDto SellingUnit);

public sealed record SellingUnitDto(string UnitName, decimal? WeightKg);

/// <summary>Tag assigned to a market product listing, projected from the catalog.</summary>
public sealed record MarketProductTagDto(Guid Id, string Name, bool PinsToTop);

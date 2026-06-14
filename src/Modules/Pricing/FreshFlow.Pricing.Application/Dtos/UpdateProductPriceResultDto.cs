namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Response DTO returned after a successful price/quantity update.
/// Corresponds to PATCH /api/v1/markets/{marketId}/products/{productId}/price response.
/// </summary>
public sealed record UpdateProductPriceResultDto(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    decimal PreviousPrice,
    decimal CurrentPrice,
    int CurrentQuantity,
    decimal ChangePercent,
    DateTime UpdatedAt,
    Guid? UpdatedBy,
    Guid SnapshotId);

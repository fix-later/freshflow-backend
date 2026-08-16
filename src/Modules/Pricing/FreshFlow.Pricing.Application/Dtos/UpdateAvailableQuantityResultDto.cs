namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// UC-PRI-04 response: reflects the state of the market product after a quantity update.
/// </summary>
public sealed record UpdateAvailableQuantityResultDto(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    int PreviousQuantity,
    int CurrentQuantity,
    bool IsOutOfStock,
    DateTime UpdatedAt,
    Guid? UpdatedBy);

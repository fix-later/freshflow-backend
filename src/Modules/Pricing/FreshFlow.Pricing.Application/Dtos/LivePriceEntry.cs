namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Live price/quantity data from the <c>price:{marketId}:{productId}</c> Redis hash.
/// </summary>
public sealed record LivePriceEntry(
    decimal Price,
    int Quantity,

    // TODO: subtract soft-reserved from Redis order:reservation:{marketProductId}
    // when the Orders module reservation counter is integrated.
    // For v1, AvailableQuantity = Quantity (no soft-reservation source yet).
    int AvailableQuantity);

namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Live price/quantity data for a single market product from the price board.
/// For v1, sourced by <c>DbPriceBoardReader</c> reading directly from <c>market_products</c>.
/// TODO: when the Redis-backed reader is ready (UC-PRI-07), this is populated from
///       the <c>price:{marketId}:{productId}</c> hash instead.
/// </summary>
public sealed record LivePriceEntry(
    decimal Price,
    int Quantity,

    // TODO: subtract soft-reserved from Redis order:reservation:{marketProductId}
    // when the Orders module reservation counter is integrated.
    // For v1, AvailableQuantity = Quantity (no soft-reservation source yet).
    int AvailableQuantity);

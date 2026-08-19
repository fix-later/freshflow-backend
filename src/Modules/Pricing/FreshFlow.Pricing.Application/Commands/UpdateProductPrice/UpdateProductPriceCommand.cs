using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.UpdateProductPrice;

/// <summary>
/// PATCH /api/v1/markets/{marketId}/products/{productId}/price
/// Updates the current price and/or available quantity of a product at a market.
/// At least one of <see cref="Price"/> or <see cref="Quantity"/> must be provided.
/// </summary>
/// <param name="MarketId">Market where the product is listed (from path).</param>
/// <param name="ProductId">Catalog product (from path).</param>
/// <param name="AgentUserId">Authenticated market agent (from JWT claim).</param>
/// <param name="Price">New price in VND. If null, price is not changed.</param>
/// <param name="Quantity">New procurement quantity. If null, quantity is not changed.</param>
/// <param name="ExpectedVersion">
///   Optional optimistic-concurrency token.
///   Must match the current <c>UpdatedAt</c> of the market_products row.
/// </param>
/// <param name="IsAdmin">True when the caller holds the admin role; bypasses the market-assignment guard.</param>
public sealed record UpdateProductPriceCommand(
    Guid MarketId,
    Guid ProductId,
    Guid AgentUserId,
    decimal? Price,
    int? Quantity,
    DateTime? ExpectedVersion,
    bool IsAdmin = false) : ICommand<UpdateProductPriceResultDto>;

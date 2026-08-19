using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;

/// <summary>
/// PATCH /api/v1/markets/{marketId}/products/{productId}/quantity
/// Sets the available procurement quantity for a product at a market.
/// quantity=0 is valid (marks product as OUT_OF_STOCK but keeps it visible).
/// </summary>
/// <param name="MarketId">Market where the product is listed (from path).</param>
/// <param name="ProductId">Catalog product (from path).</param>
/// <param name="AgentUserId">Authenticated market agent (from JWT claim).</param>
/// <param name="Quantity">New procurement quantity (must be ≥ 0, enforced by handler as 422).</param>
/// <param name="ExpectedVersion">
///   Optional optimistic-concurrency token.
///   Must match the current <c>UpdatedAt</c> of the market_products row.
/// </param>
/// <param name="IsAdmin">True when the caller holds the admin role; bypasses the market-assignment guard.</param>
public sealed record UpdateAvailableQuantityCommand(
    Guid MarketId,
    Guid ProductId,
    Guid AgentUserId,
    int Quantity,
    DateTime? ExpectedVersion,
    bool IsAdmin = false) : ICommand<UpdateAvailableQuantityResultDto>;

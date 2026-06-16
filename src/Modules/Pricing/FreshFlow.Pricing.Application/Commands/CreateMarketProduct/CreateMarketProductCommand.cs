using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.CreateMarketProduct;

/// <summary>
/// SCRUM-178 — POST /api/v1/markets/{marketId}/products
/// Admin lists a catalog product at a market with an initial price and quantity.
/// </summary>
/// <param name="MarketId">Market the product is being listed at (from path).</param>
/// <param name="ProductId">Catalog product being listed (from body).</param>
/// <param name="InitialPrice">Starting price in VND. Must be greater than 0.</param>
/// <param name="InitialQuantity">Starting procurement quantity. Must be non-negative.</param>
/// <param name="CreatedBy">Authenticated admin user (from JWT claim).</param>
public sealed record CreateMarketProductCommand(
    Guid MarketId,
    Guid ProductId,
    decimal InitialPrice,
    int InitialQuantity,
    Guid CreatedBy) : ICommand<CreateMarketProductResultDto>;

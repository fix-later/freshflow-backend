using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.GetPriceChangeHistory;

/// <summary>
/// Returns a paginated price/quantity change history for a specific product at a market.
/// Endpoint: GET /api/v1/markets/{marketId}/products/{productId}/price-history [any authenticated].
/// </summary>
public sealed record GetPriceChangeHistoryQuery(
    Guid MarketId,
    Guid ProductId,
    string? Cursor = null,
    int PageSize = 50,
    DateTime? From = null,
    DateTime? To = null)
    : IQuery<PriceHistoryPageDto>;

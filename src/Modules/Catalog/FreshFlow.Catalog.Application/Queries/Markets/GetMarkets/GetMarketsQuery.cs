using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;

public sealed record GetMarketsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<MarketDto>>;

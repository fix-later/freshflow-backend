using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Queries.Markets.GetMarketById;

public sealed record GetMarketByIdQuery(Guid Id) : IQuery<MarketDto>;

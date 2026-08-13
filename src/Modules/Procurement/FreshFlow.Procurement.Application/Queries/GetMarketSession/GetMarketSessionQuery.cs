using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSession;

public sealed record GetMarketSessionQuery(Guid Id) : IQuery<MarketSessionDto>;

using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessionTracking;

public sealed record GetMarketSessionTrackingQuery(
    Guid Id,
    int Page = 1,
    int PageSize = 50) : IQuery<MarketSessionTrackingDto>;

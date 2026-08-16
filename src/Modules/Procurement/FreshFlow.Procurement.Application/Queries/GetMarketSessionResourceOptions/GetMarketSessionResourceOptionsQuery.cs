using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessionResourceOptions;

public sealed record GetMarketSessionResourceOptionsQuery(Guid SessionId)
    : IQuery<MarketSessionResourcesDto>;

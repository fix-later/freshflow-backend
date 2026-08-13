using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessions;

public sealed record GetMarketSessionsQuery(
    DateOnly? From, DateOnly? To, Guid? MarketId, string? Status) : IQuery<IReadOnlyList<MarketSessionDto>>;

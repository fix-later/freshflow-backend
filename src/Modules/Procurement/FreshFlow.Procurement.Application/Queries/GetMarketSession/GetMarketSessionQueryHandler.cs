using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSession;

internal sealed class GetMarketSessionQueryHandler(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    MarketSessionLifecycleService lifecycle)
    : IRequestHandler<GetMarketSessionQuery, Result<MarketSessionDto>>
{
    public async Task<Result<MarketSessionDto>> Handle(GetMarketSessionQuery request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.Id, ct);
        if (session is null)
            return Result<MarketSessionDto>.Failure(Error.NotFound("MARKET_SESSION", request.Id));
        var names = await markets.ReadMarketCodesAsync([session.MarketId], ct);
        var readiness = await lifecycle.ReadReadinessAsync(
            session.MarketId, session.HubId, session.ServiceDate, ct);
        return Result<MarketSessionDto>.Success(GetMarketSessionsQueryHandler.ToDto(
            session, names.GetValueOrDefault(session.MarketId).Name, readiness));
    }
}

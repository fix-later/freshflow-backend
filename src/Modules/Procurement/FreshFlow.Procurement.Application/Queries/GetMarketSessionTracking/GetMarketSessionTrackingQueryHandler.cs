using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessionTracking;

internal sealed class GetMarketSessionTrackingQueryHandler(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    MarketSessionLifecycleService lifecycle,
    IMarketSessionTrackingReader tracking)
    : IRequestHandler<GetMarketSessionTrackingQuery, Result<MarketSessionTrackingDto>>
{
    public async Task<Result<MarketSessionTrackingDto>> Handle(
        GetMarketSessionTrackingQuery request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.Id, ct);
        if (session is null)
            return Result<MarketSessionTrackingDto>.Failure(
                Error.NotFound("MARKET_SESSION", request.Id));

        var data = await tracking.ReadAsync(request.Id, request.Page, request.PageSize, ct);
        var names = await markets.ReadMarketCodesAsync([session.MarketId], ct);
        var readiness = await lifecycle.ReadReadinessAsync(session, ct);

        return Result<MarketSessionTrackingDto>.Success(new(
            GetMarketSessionsQueryHandler.ToDto(
                session, names.GetValueOrDefault(session.MarketId).Name, readiness),
            data.Summary,
            data.Products,
            data.Orders,
            data.OrdersPagination,
            data.Batch));
    }
}

using System.Text.Json;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.OpenMarketSession;

internal sealed class OpenMarketSessionCommandHandler(
    IMarketSessionRepository sessions,
    IHubByMarketReader hubs,
    IMarketCodeReader markets,
    MarketSessionLifecycleService lifecycle,
    IAuditLogWriter auditLogs,
    TimeProvider timeProvider)
    : IRequestHandler<OpenMarketSessionCommand, Result<MarketSessionDto>>
{
    public async Task<Result<MarketSessionDto>> Handle(OpenMarketSessionCommand request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.Id, ct);
        if (session is null)
            return Result<MarketSessionDto>.Failure(Error.NotFound("MARKET_SESSION", request.Id));

        if (session.Status == MarketSessionStatus.Open)
        {
            var currentReadiness = await lifecycle.ReadReadinessAsync(session, ct);
            var currentNames = await markets.ReadMarketCodesAsync([session.MarketId], ct);
            return Result<MarketSessionDto>.Success(GetMarketSessionsQueryHandler.ToDto(
                session, currentNames.GetValueOrDefault(session.MarketId).Name, currentReadiness));
        }

        var activeHubs = await hubs.ReadActiveHubsAsync([session.MarketId], ct);
        if (!activeHubs.TryGetValue(session.MarketId, out var hubId))
            return NotReady("HUB_NOT_CONFIGURED");
        var readiness = session.Vehicles.Count > 0 || session.Agents.Count > 0
            ? await lifecycle.ReadReadinessAsync(session, ct)
            : await lifecycle.ReadReadinessAsync(session.MarketId, hubId, session.ServiceDate, ct);
        if (!readiness.IsReady)
            return NotReady(string.Join(',', readiness.Warnings));

        var open = session.Open(hubId, timeProvider.GetUtcNow().UtcDateTime);
        if (open.IsFailure)
            return Result<MarketSessionDto>.Failure(open.Error);
        if (!await sessions.SaveChangesAsync(ct))
            return Result<MarketSessionDto>.Failure(Error.Conflict(
                "MARKET_SESSION_CONFLICT", "The market session changed concurrently. Retry."));

        await auditLogs.WriteAsync(
            request.ActorId,
            "market_session_opened",
            "market_session",
            session.Id,
            JsonSerializer.Serialize(new { session.MarketId, session.ServiceDate, session.HubId }),
            timeProvider.GetUtcNow().UtcDateTime,
            ct);

        var names = await markets.ReadMarketCodesAsync([session.MarketId], ct);
        return Result<MarketSessionDto>.Success(GetMarketSessionsQueryHandler.ToDto(
            session, names.GetValueOrDefault(session.MarketId).Name, readiness));
    }

    private static Result<MarketSessionDto> NotReady(string warning) =>
        Result<MarketSessionDto>.Failure(Error.Validation(
            "MARKET_SESSION_NOT_READY", $"The market session is not ready: {warning}."));
}

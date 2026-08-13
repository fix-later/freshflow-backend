using System.Text.Json;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.UpdateMarketSession;

internal sealed class UpdateMarketSessionCommandHandler(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    MarketSessionLifecycleService lifecycle,
    IAuditLogWriter auditLogs,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateMarketSessionCommand, Result<MarketSessionDto>>
{
    public async Task<Result<MarketSessionDto>> Handle(UpdateMarketSessionCommand request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.Id, ct);
        if (session is null)
            return Result<MarketSessionDto>.Failure(Error.NotFound("MARKET_SESSION", request.Id));
        var previousClosesAt = session.ClosesAt;
        var update = session.UpdateSchedule(request.ClosesAt.UtcDateTime, timeProvider.GetUtcNow().UtcDateTime);
        if (update.IsFailure)
            return Result<MarketSessionDto>.Failure(update.Error);
        if (!await sessions.SaveChangesAsync(ct))
            return Conflict();

        await auditLogs.WriteAsync(
            request.ActorId,
            "market_session_schedule_updated",
            "market_session",
            session.Id,
            JsonSerializer.Serialize(new { before = previousClosesAt, after = session.ClosesAt }),
            timeProvider.GetUtcNow().UtcDateTime,
            ct);

        var names = await markets.ReadMarketCodesAsync([session.MarketId], ct);
        var readiness = await lifecycle.ReadReadinessAsync(
            session.MarketId, session.HubId, session.ServiceDate, ct);
        return Result<MarketSessionDto>.Success(GetMarketSessionsQueryHandler.ToDto(
            session, names.GetValueOrDefault(session.MarketId).Name, readiness));
    }

    private static Result<MarketSessionDto> Conflict() => Result<MarketSessionDto>.Failure(Error.Conflict(
        "MARKET_SESSION_CONFLICT", "The market session changed concurrently. Retry."));
}

using System.Text.Json;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.CloseMarketSession;

internal sealed class CloseMarketSessionCommandHandler(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    IProcurementBatchingService batching,
    MarketSessionLifecycleService lifecycle,
    IAuditLogWriter auditLogs,
    TimeProvider timeProvider)
    : IRequestHandler<CloseMarketSessionCommand, Result<MarketSessionDto>>
{
    public async Task<Result<MarketSessionDto>> Handle(CloseMarketSessionCommand request, CancellationToken ct)
    {
        if (request.Reason?.Length > 500)
            return Result<MarketSessionDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "Reason cannot exceed 500 characters."));

        MarketSession? closed = null;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var transaction = await sessions.ExecuteInTransactionAsync(async txCt =>
        {
            closed = await sessions.FindForUpdateAsync(request.Id, txCt);
            if (closed is null)
                return Result.Failure(Error.NotFound("MARKET_SESSION", request.Id));
            return closed.Close(request.ActorId, request.Reason, nowUtc);
        }, ct);
        if (transaction.IsFailure)
            return Result<MarketSessionDto>.Failure(transaction.Error);

        await auditLogs.WriteAsync(
            request.ActorId,
            "market_session_closed",
            "market_session",
            closed!.Id,
            JsonSerializer.Serialize(new { closed.MarketId, closed.ServiceDate, reason = closed.CloseReason }),
            nowUtc,
            ct);

        await batching.BuildSessionBatchAsync(closed.Id, false, ct); // failure stays pending for hosted retry
        var refreshed = await sessions.FindByIdAsync(closed.Id, ct) ?? closed;
        var names = await markets.ReadMarketCodesAsync([closed.MarketId], ct);
        var readiness = await lifecycle.ReadReadinessAsync(
            closed.MarketId, closed.HubId, closed.ServiceDate, ct);
        return Result<MarketSessionDto>.Success(GetMarketSessionsQueryHandler.ToDto(
            refreshed, names.GetValueOrDefault(closed.MarketId).Name, readiness));
    }
}

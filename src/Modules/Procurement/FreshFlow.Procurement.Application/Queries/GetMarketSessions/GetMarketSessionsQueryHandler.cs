using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessions;

internal sealed class GetMarketSessionsQueryHandler(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    MarketSessionLifecycleService lifecycle)
    : IRequestHandler<GetMarketSessionsQuery, Result<IReadOnlyList<MarketSessionDto>>>
{
    public async Task<Result<IReadOnlyList<MarketSessionDto>>> Handle(
        GetMarketSessionsQuery request, CancellationToken ct)
    {
        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            return Result<IReadOnlyList<MarketSessionDto>>.Failure(Error.Validation(
                "VALIDATION_ERROR", "From must be on or before To."));
        if (request.Status is not null &&
            !Enum.TryParse<MarketSessionStatus>(request.Status, true, out _))
            return Result<IReadOnlyList<MarketSessionDto>>.Failure(Error.Validation(
                "VALIDATION_ERROR", "Status must be draft, open, or closed."));

        MarketSessionStatus? status = request.Status is null
            ? null
            : Enum.Parse<MarketSessionStatus>(request.Status, true);
        var rows = await sessions.ListAsync(request.From, request.To, request.MarketId, status, ct);
        var names = await markets.ReadMarketCodesAsync(rows.Select(row => row.MarketId).Distinct().ToArray(), ct);
        var result = new List<MarketSessionDto>(rows.Count);
        foreach (var row in rows)
        {
            var readiness = await lifecycle.ReadReadinessAsync(row, ct);
            result.Add(ToDto(row, names.GetValueOrDefault(row.MarketId).Name, readiness));
        }

        return Result<IReadOnlyList<MarketSessionDto>>.Success(result);
    }

    internal static MarketSessionDto ToDto(
        MarketSession session, string? marketName, MarketSessionReadiness readiness) => new(
        session.Id,
        session.MarketId,
        marketName,
        session.HubId,
        session.ServiceDate,
        session.Status.ToString().ToLowerInvariant(),
        session.ClosesAt,
        session.ClosedAt,
        session.ClosedBy,
        session.CloseReason,
        session.BatchingCompletedAt,
        readiness.EligibleAgentCount,
        readiness.AvailableVehicleCount,
        readiness.HubVehicleCapacityKg,
        readiness.ReferenceVehicleCapacityKg,
        session.PlannedCapacityKg,
        session.Vehicles.Select(row => row.VehicleId).ToList().AsReadOnly(),
        session.Agents.Select(row => row.UserId).ToList().AsReadOnly(),
        readiness.IsReady ? "ready" : session.Status == MarketSessionStatus.Draft ? "blocked" : "warning",
        readiness.Warnings,
        session.CreatedAt,
        session.UpdatedAt);
}

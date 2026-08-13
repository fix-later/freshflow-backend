using System.Text.Json;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessionResourceOptions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.ConfigureMarketSessionResources;

internal sealed class ConfigureMarketSessionResourcesCommandHandler(
    IMarketSessionRepository sessions,
    IMarketSessionReadinessReader vehicles,
    IMarketAgentReader agents,
    IAuditLogWriter auditLogs,
    TimeProvider timeProvider)
    : IRequestHandler<ConfigureMarketSessionResourcesCommand, Result<MarketSessionResourcesDto>>
{
    public async Task<Result<MarketSessionResourcesDto>> Handle(
        ConfigureMarketSessionResourcesCommand request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, ct);
        if (session is null)
            return Result<MarketSessionResourcesDto>.Failure(Error.NotFound("MARKET_SESSION", request.SessionId));

        var options = await GetMarketSessionResourceOptionsQueryHandler.MapAsync(session, vehicles, agents, ct);
        var availableVehicleIds = options.Vehicles.Select(vehicle => vehicle.VehicleId).ToHashSet();
        var eligibleAgentIds = options.Agents.Select(agent => agent.UserId).ToHashSet();
        if (request.VehicleIds.Distinct().Any(id => !availableVehicleIds.Contains(id)))
            return Invalid("Every vehicle must be available and belong to the session hub.");
        if (request.AgentUserIds.Distinct().Any(id => !eligibleAgentIds.Contains(id)))
            return Invalid("Every agent must be active and assigned to the session market.");

        var configuredAt = timeProvider.GetUtcNow().UtcDateTime;
        var configure = session.ConfigureResources(
            request.PlannedCapacityKg,
            request.VehicleIds,
            request.AgentUserIds,
            request.ActorId,
            configuredAt);
        if (configure.IsFailure)
            return Result<MarketSessionResourcesDto>.Failure(configure.Error);
        if (!await sessions.SaveChangesAsync(ct))
            return Result<MarketSessionResourcesDto>.Failure(Error.Conflict(
                "MARKET_SESSION_CONFLICT", "The market session changed concurrently. Retry."));

        await auditLogs.WriteAsync(
            request.ActorId,
            "market_session_resources_configured",
            "market_session",
            session.Id,
            JsonSerializer.Serialize(new
            {
                request.PlannedCapacityKg,
                vehicleIds = request.VehicleIds.Distinct(),
                agentUserIds = request.AgentUserIds.Distinct()
            }),
            configuredAt,
            ct);

        return Result<MarketSessionResourcesDto>.Success(
            await GetMarketSessionResourceOptionsQueryHandler.MapAsync(session, vehicles, agents, ct));
    }

    private static Result<MarketSessionResourcesDto> Invalid(string message) =>
        Result<MarketSessionResourcesDto>.Failure(
            Error.Validation("INVALID_MARKET_SESSION_RESOURCES", message));
}

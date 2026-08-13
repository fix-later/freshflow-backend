using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessionResourceOptions;

internal sealed class GetMarketSessionResourceOptionsQueryHandler(
    IMarketSessionRepository sessions,
    IMarketSessionReadinessReader vehicles,
    IMarketAgentReader agents)
    : IRequestHandler<GetMarketSessionResourceOptionsQuery, Result<MarketSessionResourcesDto>>
{
    public async Task<Result<MarketSessionResourcesDto>> Handle(
        GetMarketSessionResourceOptionsQuery request, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, ct);
        if (session is null)
            return Result<MarketSessionResourcesDto>.Failure(Error.NotFound("MARKET_SESSION", request.SessionId));

        return Result<MarketSessionResourcesDto>.Success(await MapAsync(session, vehicles, agents, ct));
    }

    internal static async Task<MarketSessionResourcesDto> MapAsync(
        MarketSession session,
        IMarketSessionReadinessReader vehicles,
        IMarketAgentReader agents,
        CancellationToken ct)
    {
        var available = session.HubId.HasValue
            ? await vehicles.ReadVehicleAvailabilityAsync(session.HubId.Value, session.ServiceDate, ct)
            : new VehicleAvailabilityDto(0, 0m);
        var eligibleAgents = await agents.ListEligibleMarketAgentsAsync(session.MarketId, ct);
        var selectedVehicleIds = session.Vehicles.Select(row => row.VehicleId).ToHashSet();
        var selectedAgentIds = session.Agents.Select(row => row.UserId).ToHashSet();
        var options = available.Vehicles ?? [];

        return new MarketSessionResourcesDto(
            session.Id,
            session.PlannedCapacityKg,
            available.CapacityKg,
            options.Where(vehicle => selectedVehicleIds.Contains(vehicle.VehicleId))
                .Sum(vehicle => vehicle.CapacityKg),
            options.Select(vehicle => new MarketSessionResourceVehicleDto(
                    vehicle.VehicleId,
                    vehicle.PlateNumber,
                    vehicle.CapacityKg,
                    vehicle.VehicleType,
                    selectedVehicleIds.Contains(vehicle.VehicleId)))
                .ToList()
                .AsReadOnly(),
            eligibleAgents.Select(agent => new MarketSessionResourceAgentDto(
                    agent.UserId,
                    agent.Email,
                    agent.FullName,
                    selectedAgentIds.Contains(agent.UserId)))
                .ToList()
                .AsReadOnly());
    }
}

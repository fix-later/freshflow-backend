using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.ConfigureMarketSessionResources;

public sealed record ConfigureMarketSessionResourcesCommand(
    Guid SessionId,
    decimal PlannedCapacityKg,
    IReadOnlyList<Guid> VehicleIds,
    IReadOnlyList<Guid> AgentUserIds,
    Guid ActorId) : ICommand<MarketSessionResourcesDto>;

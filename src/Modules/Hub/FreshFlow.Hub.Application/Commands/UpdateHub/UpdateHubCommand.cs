using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.UpdateHub;

public sealed record UpdateHubCommand(
    Guid HubId,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal CapacityKg,
    Guid? ManagedBy,
    Guid? MarketId) : ICommand<HubDto>;

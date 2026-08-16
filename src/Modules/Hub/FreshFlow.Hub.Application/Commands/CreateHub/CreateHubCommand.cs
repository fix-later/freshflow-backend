using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.CreateHub;

public sealed record CreateHubCommand(
    Guid MarketId,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal CapacityKg,
    Guid? ManagedBy) : ICommand<HubDto>;

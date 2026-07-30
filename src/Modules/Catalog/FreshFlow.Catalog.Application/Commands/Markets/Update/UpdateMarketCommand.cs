using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Markets.Update;

public sealed record UpdateMarketCommand(
    Guid Id,
    string Name,
    string? Location,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? ImageUrl = null,
    string? Description = null) : ICommand<MarketDto>;

using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Markets.Create;

public sealed record CreateMarketCommand(
    string Name,
    string? Location,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? ImageUrl = null,
    string? Description = null) : ICommand<MarketDto>;

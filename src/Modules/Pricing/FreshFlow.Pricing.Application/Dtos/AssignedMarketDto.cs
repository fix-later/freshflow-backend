namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>Market summary returned to a market agent for their assigned markets.</summary>
public sealed record AssignedMarketDto(
    Guid MarketId,
    string Name,
    string? Location,
    string? Address);

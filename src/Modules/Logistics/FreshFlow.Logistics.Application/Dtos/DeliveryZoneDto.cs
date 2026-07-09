namespace FreshFlow.Logistics.Application.Dtos;

public sealed record DeliveryZoneDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

namespace FreshFlow.Hub.Application.Abstractions;

public interface IDeliveryRouteReader
{
    public Task<DeliveryRouteLookupDto?> FindByIdAsync(Guid routeId, CancellationToken ct);
}

public sealed record DeliveryRouteLookupDto(Guid RouteId, string Status, Guid? DriverUserId);

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDriverReader
{
    public Task<DriverDto?> FindByUserIdAsync(Guid userId, CancellationToken ct);
}

public sealed record DriverDto(Guid UserId, string RoleName, bool IsActive);

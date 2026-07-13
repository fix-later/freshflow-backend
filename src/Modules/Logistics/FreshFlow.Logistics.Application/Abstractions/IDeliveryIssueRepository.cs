using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryIssueRepository
{
    public Task AddAsync(DeliveryIssue issue, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}

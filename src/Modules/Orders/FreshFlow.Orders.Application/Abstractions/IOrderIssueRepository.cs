using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOrderIssueRepository
{
    public Task AddAsync(OrderIssue issue, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}

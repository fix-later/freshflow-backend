using FreshFlow.Orders.Application.Dtos;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IScheduledOrderGenerationService
{
    public Task<ScheduledOrderGenerationResultDto> GenerateDueAsync(
        DateTime utcNow, CancellationToken ct);
}

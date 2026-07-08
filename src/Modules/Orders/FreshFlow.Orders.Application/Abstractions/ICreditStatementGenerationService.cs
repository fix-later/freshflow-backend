using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>
/// Generates (or returns the existing) immutable monthly credit statement for a restaurant.
/// Shared by <c>GenerateCreditStatementCommandHandler</c> (authenticated request, RBAC
/// already checked by the caller) and the monthly background job (system-trusted, no RBAC)
/// — mirrors the <see cref="IScheduledOrderGenerationService"/> split between an
/// authenticated command and a hosted-service-driven batch run.
/// </summary>
public interface ICreditStatementGenerationService
{
    public Task<Result<CreditStatementDto>> GenerateAsync(
        Guid restaurantId, int year, int month, CancellationToken ct);
}

using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.ListEligibleDrivers;

internal sealed class ListEligibleDriversQueryHandler(IDriverReader drivers)
    : IRequestHandler<ListEligibleDriversQuery, Result<IReadOnlyList<DriverDto>>>
{
    public async Task<Result<IReadOnlyList<DriverDto>>> Handle(
        ListEligibleDriversQuery request, CancellationToken ct)
    {
        var eligible = await drivers.ListEligibleAsync(ct);
        return Result<IReadOnlyList<DriverDto>>.Success(eligible);
    }
}

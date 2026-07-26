using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHubProcurementPlan;

internal sealed class GetHubProcurementPlanQueryHandler(IHubProcurementPlanReader reader)
    : IRequestHandler<GetHubProcurementPlanQuery, Result<HubProcurementPlanDto>>
{
    public async Task<Result<HubProcurementPlanDto>> Handle(
        GetHubProcurementPlanQuery request,
        CancellationToken ct) =>
        Result<HubProcurementPlanDto>.Success(
            await reader.ReadAsync(request.HubId, request.Date, ct));
}

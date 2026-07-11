using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListDiscrepancies;

internal sealed class ListDiscrepanciesQueryHandler(
    IHubRepository hubs,
    IHubDiscrepancyRepository discrepancies)
    : IRequestHandler<ListDiscrepanciesQuery, Result<HubDiscrepancyPageDto>>
{
    public async Task<Result<HubDiscrepancyPageDto>> Handle(ListDiscrepanciesQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubDiscrepancyPageDto>.Failure(Error.NotFound("HUB", request.HubId));

        var (items, nextCursor) = await discrepancies.GetPageAsync(
            request.HubId,
            request.Status,
            request.Cursor,
            request.PageSize,
            ct);

        return Result<HubDiscrepancyPageDto>.Success(new HubDiscrepancyPageDto(
            items.Select(item => item.ToDto()).ToList().AsReadOnly(),
            request.PageSize,
            nextCursor));
    }
}

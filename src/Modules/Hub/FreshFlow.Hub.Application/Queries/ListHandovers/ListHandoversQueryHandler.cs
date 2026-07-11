using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListHandovers;

internal sealed class ListHandoversQueryHandler(
    IHubRepository hubs,
    IHubHandoverRepository handovers)
    : IRequestHandler<ListHandoversQuery, Result<HubHandoverPageDto>>
{
    public async Task<Result<HubHandoverPageDto>> Handle(ListHandoversQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubHandoverPageDto>.Failure(Error.NotFound("HUB", request.HubId));

        var (items, nextCursor) = await handovers.GetPageAsync(
            request.HubId,
            request.Cursor,
            request.PageSize,
            ct);

        var dtos = items.Select(handover => handover.ToDto()).ToList().AsReadOnly();
        return Result<HubHandoverPageDto>.Success(new HubHandoverPageDto(dtos, request.PageSize, nextCursor));
    }
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListInbound;

internal sealed class ListInboundQueryHandler(
    IHubRepository hubs,
    IHubInboundRepository inbounds)
    : IRequestHandler<ListInboundQuery, Result<HubInboundPageDto>>
{
    public async Task<Result<HubInboundPageDto>> Handle(ListInboundQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubInboundPageDto>.Failure(Error.NotFound("HUB", request.HubId));

        var (items, nextCursor, totalQuantityKg) = await inbounds.GetHistoryPageAsync(
            request.HubId,
            request.Date,
            request.Cursor,
            request.PageSize,
            ct);

        var dtos = items.Select(inbound => inbound.ToDto()).ToList().AsReadOnly();
        return Result<HubInboundPageDto>.Success(
            new HubInboundPageDto(dtos, request.PageSize, nextCursor, totalQuantityKg));
    }
}

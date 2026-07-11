using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListOutbound;

internal sealed class ListOutboundQueryHandler(
    IHubRepository hubs,
    IHubOutboundRepository outbounds)
    : IRequestHandler<ListOutboundQuery, Result<HubOutboundPageDto>>
{
    public async Task<Result<HubOutboundPageDto>> Handle(ListOutboundQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubOutboundPageDto>.Failure(Error.NotFound("HUB", request.HubId));

        var (items, nextCursor, totalQuantityKg) = await outbounds.GetHistoryPageAsync(
            request.HubId,
            request.Date,
            request.Cursor,
            request.PageSize,
            ct);

        var dtos = items.Select(outbound => outbound.ToDto()).ToList().AsReadOnly();
        return Result<HubOutboundPageDto>.Success(
            new HubOutboundPageDto(dtos, request.PageSize, nextCursor, totalQuantityKg));
    }
}

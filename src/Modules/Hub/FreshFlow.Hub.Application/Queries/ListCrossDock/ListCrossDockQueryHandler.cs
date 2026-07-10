using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListCrossDock;

internal sealed class ListCrossDockQueryHandler(
    IHubRepository hubs,
    ICrossDockRepository transfers)
    : IRequestHandler<ListCrossDockQuery, Result<CrossDockTransferPageDto>>
{
    public async Task<Result<CrossDockTransferPageDto>> Handle(ListCrossDockQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<CrossDockTransferPageDto>.Failure(Error.NotFound("HUB", request.HubId));

        var (items, nextCursor) = await transfers.GetPageAsync(
            request.HubId,
            request.Status,
            request.Cursor,
            request.PageSize,
            ct);

        var dtos = items.Select(transfer => transfer.ToDto()).ToList().AsReadOnly();
        return Result<CrossDockTransferPageDto>.Success(
            new CrossDockTransferPageDto(dtos, request.PageSize, nextCursor));
    }
}

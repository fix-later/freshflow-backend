using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.ListHubs;

internal sealed class ListHubsQueryHandler(IHubRepository hubs)
    : IRequestHandler<ListHubsQuery, Result<HubPageDto>>
{
    public async Task<Result<HubPageDto>> Handle(ListHubsQuery request, CancellationToken ct)
    {
        var (items, nextCursor) = await hubs.GetPageAsync(
            request.Cursor,
            request.PageSize,
            request.IsActive,
            ct);

        var dtos = items.Select(hub => hub.ToDto()).ToList().AsReadOnly();
        return Result<HubPageDto>.Success(new HubPageDto(dtos, request.PageSize, nextCursor));
    }
}

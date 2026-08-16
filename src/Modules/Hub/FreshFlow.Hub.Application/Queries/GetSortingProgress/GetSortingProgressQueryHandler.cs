using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetSortingProgress;

internal sealed class GetSortingProgressQueryHandler(
    IHubRepository hubs,
    IHubSortingProgressRepository progress,
    IOrderLookupReader orders)
    : IRequestHandler<GetSortingProgressQuery, Result<IReadOnlyList<HubSortingProgressDto>>>
{
    public async Task<Result<IReadOnlyList<HubSortingProgressDto>>> Handle(
        GetSortingProgressQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<IReadOnlyList<HubSortingProgressDto>>.Failure(Error.NotFound("HUB", request.HubId));

        var lines = await progress.ListByHubAndDateAsync(request.HubId, request.ServiceDate, ct);
        var ordersByItem = (await orders.FindByOrderItemIdsAsync(
                lines.Select(line => line.OrderItemId).ToList(), ct))
            .ToDictionary(order => order.OrderItemId);


        return Result<IReadOnlyList<HubSortingProgressDto>>.Success(
            lines.Select(line => line.ToDto(ordersByItem.GetValueOrDefault(line.OrderItemId)))
                .ToList().AsReadOnly());
    }
}

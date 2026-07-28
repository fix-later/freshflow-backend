using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.MarkLineSorted;

internal sealed class MarkLineSortedCommandHandler(
    IHubRepository hubs,
    IHubSortingProgressRepository progress)
    : IRequestHandler<MarkLineSortedCommand, Result<HubSortingProgressDto>>
{
    public async Task<Result<HubSortingProgressDto>> Handle(MarkLineSortedCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubSortingProgressDto>.Failure(Error.NotFound("HUB", request.HubId));

        // Upsert on (HubId, ServiceDate, OrderItemId): a second POST for the same line overwrites the
        // sorted quantity/who/when instead of creating a second row.
        var line = await progress.FindByHubDateAndOrderItemAsync(
            request.HubId, request.ServiceDate, request.OrderItemId, ct);
        if (line is null)
        {
            line = HubSortingProgress.Create(request.HubId, request.ServiceDate, request.OrderItemId);
            await progress.AddAsync(line, ct);
        }

        line.MarkSorted(request.SortedQuantityKg, request.ActorUserId, DateTime.UtcNow);

        await progress.SaveChangesAsync(ct);

        return Result<HubSortingProgressDto>.Success(line.ToDto());
    }
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Application.Commands.CreateHub;

internal sealed class CreateHubCommandHandler(IHubRepository hubs)
    : IRequestHandler<CreateHubCommand, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(CreateHubCommand request, CancellationToken ct)
    {
        var hub = HubEntity.Create(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.CapacityKg,
            request.ManagedBy);

        await hubs.AddAsync(hub, ct);
        try
        {
            await hubs.SaveChangesAsync(ct);
        }
        catch (HubConcurrencyException)
        {
            return Result<HubDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "Hub save conflicted with another request. Please refresh and retry."));
        }

        return Result<HubDto>.Success(hub.ToDto());
    }
}

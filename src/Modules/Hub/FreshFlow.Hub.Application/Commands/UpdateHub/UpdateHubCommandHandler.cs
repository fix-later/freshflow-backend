using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.UpdateHub;

internal sealed class UpdateHubCommandHandler(IHubRepository hubs)
    : IRequestHandler<UpdateHubCommand, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(UpdateHubCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubDto>.Failure(Error.NotFound("HUB", request.HubId));

        hub.Update(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.CapacityKg,
            request.ManagedBy);

        await hubs.SaveChangesAsync(ct);

        return Result<HubDto>.Success(hub.ToDto());
    }
}

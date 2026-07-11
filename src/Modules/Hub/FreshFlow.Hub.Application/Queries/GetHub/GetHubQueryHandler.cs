using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHub;

internal sealed class GetHubQueryHandler(IHubRepository hubs)
    : IRequestHandler<GetHubQuery, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(GetHubQuery request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        return hub is null
            ? Result<HubDto>.Failure(Error.NotFound("HUB", request.HubId))
            : Result<HubDto>.Success(hub.ToDto());
    }
}

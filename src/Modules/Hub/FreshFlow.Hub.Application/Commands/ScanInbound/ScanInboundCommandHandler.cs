using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Application.Services;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.ScanInbound;

internal sealed class ScanInboundCommandHandler(
    IHubRepository hubs,
    IHubInboundRepository inbounds,
    IHubInventoryRepository inventory,
    HubAccessChecker accessChecker)
    : IRequestHandler<ScanInboundCommand, Result<HubInboundDto>>
{
    public async Task<Result<HubInboundDto>> Handle(ScanInboundCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.Code, out var inboundId))
            return ScanNoMatch();

        var inbound = await inbounds.FindPendingByIdAsync(inboundId, ct);
        if (inbound is null)
            return ScanNoMatch();

        var hub = await hubs.FindByIdAsync(inbound.HubId, ct);
        if (hub is null)
            return Result<HubInboundDto>.Failure(Error.NotFound("HUB", inbound.HubId));

        var accessError = await accessChecker.CheckAsync(
            hub.Id,
            hub.IsActive,
            request.ActorUserId,
            request.BypassHubAssignment,
            ct);

        if (accessError is not null)
            return Result<HubInboundDto>.Failure(accessError);

        if (hub.AvailableCapacityKg < inbound.TotalQuantityKg)
        {
            return Result<HubInboundDto>.Failure(
                Error.Validation(
                    "HUB_CAPACITY_EXCEEDED",
                    "Hub capacity is not sufficient for this inbound delivery."));
        }

        inbound.ConfirmArrival(request.ActorUserId);

        foreach (var item in inbound.Items)
        {
            var current = await inventory.FindByHubAndMarketProductAsync(
                inbound.HubId,
                item.MarketProductId,
                ct);

            if (current is null)
            {
                current = HubInventory.Create(inbound.HubId, item.MarketProductId);
                await inventory.AddAsync(current, ct);
            }

            current.AddInbound(item.QuantityKg);
        }

        hub.ApplyInbound(inbound.TotalQuantityKg);
        try
        {
            await inbounds.SaveChangesAsync(ct);
        }
        catch (HubConcurrencyException)
        {
            return Result<HubInboundDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "Hub capacity was updated by another request. Please refresh and retry."));
        }

        return Result<HubInboundDto>.Success(inbound.ToDto());
    }

    private static Result<HubInboundDto> ScanNoMatch() =>
        Result<HubInboundDto>.Failure(
            Error.Validation("SCAN_NO_MATCH", "Scan code did not match a pending inbound event."));
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;

internal sealed class AcknowledgeDiscrepancyCommandHandler(IHubDiscrepancyRepository discrepancies)
    : IRequestHandler<AcknowledgeDiscrepancyCommand, Result<HubDiscrepancyDto>>
{
    public async Task<Result<HubDiscrepancyDto>> Handle(AcknowledgeDiscrepancyCommand request, CancellationToken ct)
    {
        var discrepancy = await discrepancies.FindByIdForHubAsync(request.HubId, request.DiscrepancyId, ct);
        if (discrepancy is null)
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("HUB_DISCREPANCY", request.DiscrepancyId));

        try
        {
            discrepancy.Acknowledge(request.AdminUserId);
        }
        catch (InvalidOperationException)
        {
            return Result<HubDiscrepancyDto>.Failure(Error.Conflict(
                "DISCREPANCY_ALREADY_ACKNOWLEDGED",
                "Discrepancy has already been acknowledged."));
        }

        await discrepancies.SaveChangesAsync(ct);
        return Result<HubDiscrepancyDto>.Success(discrepancy.ToDto());
    }
}

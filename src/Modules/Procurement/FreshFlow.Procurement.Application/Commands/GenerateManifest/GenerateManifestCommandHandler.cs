using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.GenerateManifest;

internal sealed class GenerateManifestCommandHandler(
    IProcurementBatchRepository batches,
    IMarketProductMarketReader marketProducts,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<GenerateManifestCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        GenerateManifestCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var marketProductIds = batch.Items
            .Select(item => item.MarketProductId)
            .Distinct()
            .ToArray();
        var prices = await marketProducts.ReadReferencePricesAsync(
            marketProductIds,
            cancellationToken);
        var manifest = batch.Manifest(
            prices,
            timeProvider.GetUtcNow().UtcDateTime);
        if (manifest.IsFailure)
            return Result<ProcurementBatchDto>.Failure(manifest.Error);

        await batches.SaveChangesAsync(cancellationToken);

        var orderIds = batch.Orders
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(batch, statuses));
    }
}

using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Create;

internal sealed class CreatePackingCodeCommandHandler(IPackingCodeRepository packingCodes, CatalogCacheEvictor evictor)
    : IRequestHandler<CreatePackingCodeCommand, Result<PackingCodeDto>>
{
    public async Task<Result<PackingCodeDto>> Handle(
        CreatePackingCodeCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        if (await packingCodes.CodeExistsAsync(code, null, ct))
            return Result<PackingCodeDto>.Failure(
                Error.Conflict("PACKING_CODE_CONFLICT", $"Packing code '{code}' already exists."));

        var packingCode = new PackingCode(code, request.Description, request.CapacityKg);
        await packingCodes.AddAsync(packingCode, ct);
        await packingCodes.SaveChangesAsync(ct);

        await evictor.EvictAsync(CatalogCache.PackingCodesTag, ct);

        return Result<PackingCodeDto>.Success(packingCode.ToDto());
    }
}

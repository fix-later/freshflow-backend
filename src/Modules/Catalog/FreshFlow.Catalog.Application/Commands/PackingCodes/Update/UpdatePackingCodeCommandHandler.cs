using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Update;

internal sealed class UpdatePackingCodeCommandHandler(IPackingCodeRepository packingCodes)
    : IRequestHandler<UpdatePackingCodeCommand, Result<PackingCodeDto>>
{
    public async Task<Result<PackingCodeDto>> Handle(
        UpdatePackingCodeCommand request, CancellationToken ct)
    {
        var packingCode = await packingCodes.FindByIdAsync(request.Id, ct);
        if (packingCode is null)
            return Result<PackingCodeDto>.Failure(Error.NotFound("PackingCode", request.Id));

        var code = request.Code.Trim();
        if (await packingCodes.CodeExistsAsync(code, request.Id, ct))
            return Result<PackingCodeDto>.Failure(
                Error.Conflict("PACKING_CODE_CONFLICT", $"Packing code '{code}' already exists."));

        packingCode.Update(code, request.Description, request.CapacityKg);
        await packingCodes.SaveChangesAsync(ct);

        return Result<PackingCodeDto>.Success(packingCode.ToDto());
    }
}

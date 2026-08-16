using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Deactivate;

internal sealed class DeactivatePackingCodeCommandHandler(IPackingCodeRepository packingCodes)
    : IRequestHandler<DeactivatePackingCodeCommand, Result<PackingCodeDto>>
{
    public async Task<Result<PackingCodeDto>> Handle(
        DeactivatePackingCodeCommand request, CancellationToken ct)
    {
        var packingCode = await packingCodes.FindByIdAsync(request.Id, ct);
        if (packingCode is null)
            return Result<PackingCodeDto>.Failure(Error.NotFound("PackingCode", request.Id));

        packingCode.Deactivate();
        await packingCodes.SaveChangesAsync(ct);

        return Result<PackingCodeDto>.Success(packingCode.ToDto());
    }
}

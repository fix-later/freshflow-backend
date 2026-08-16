using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.GetById;

internal sealed class GetPackingCodeByIdQueryHandler(IPackingCodeRepository packingCodes)
    : IRequestHandler<GetPackingCodeByIdQuery, Result<PackingCodeDto>>
{
    public async Task<Result<PackingCodeDto>> Handle(
        GetPackingCodeByIdQuery request, CancellationToken ct)
    {
        var packingCode = await packingCodes.FindByIdAsync(request.Id, ct);
        return packingCode is null
            ? Result<PackingCodeDto>.Failure(Error.NotFound("PackingCode", request.Id))
            : Result<PackingCodeDto>.Success(packingCode.ToDto());
    }
}

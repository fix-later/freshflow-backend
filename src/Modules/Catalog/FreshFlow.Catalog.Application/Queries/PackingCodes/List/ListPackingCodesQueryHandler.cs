using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.List;

internal sealed class ListPackingCodesQueryHandler(IPackingCodeRepository packingCodes)
    : IRequestHandler<ListPackingCodesQuery, Result<IReadOnlyList<PackingCodeDto>>>
{
    public async Task<Result<IReadOnlyList<PackingCodeDto>>> Handle(
        ListPackingCodesQuery request, CancellationToken ct)
    {
        var rows = await packingCodes.GetPageAsync(
            request.ActiveOnly, request.Page, request.PageSize, ct);
        return Result<IReadOnlyList<PackingCodeDto>>.Success(
            rows.Select(x => x.ToDto()).ToList().AsReadOnly());
    }
}

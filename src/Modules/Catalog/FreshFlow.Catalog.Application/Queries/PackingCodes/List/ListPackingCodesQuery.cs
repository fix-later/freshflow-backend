using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.List;

public sealed record ListPackingCodesQuery(
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<IReadOnlyList<PackingCodeDto>>>;

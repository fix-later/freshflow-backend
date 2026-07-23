using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.GetById;

public sealed record GetPackingCodeByIdQuery(Guid Id) : IRequest<Result<PackingCodeDto>>;

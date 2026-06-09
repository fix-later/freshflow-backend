using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Units.GetUnitById;

public record GetUnitByIdQuery(Guid Id) : IRequest<Result<UnitDto>>;

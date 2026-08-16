using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Units.GetUnits;

public record GetUnitsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<UnitDto>>>;

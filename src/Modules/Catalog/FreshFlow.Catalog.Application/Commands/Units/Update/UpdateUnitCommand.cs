using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Update;

public record UpdateUnitCommand(Guid Id, string Name, string? Abbreviation)
    : IRequest<Result<UnitDto>>;

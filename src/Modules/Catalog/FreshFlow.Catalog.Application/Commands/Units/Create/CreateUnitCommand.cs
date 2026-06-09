using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Create;

public record CreateUnitCommand(string Name, string? Abbreviation)
    : IRequest<Result<UnitDto>>;

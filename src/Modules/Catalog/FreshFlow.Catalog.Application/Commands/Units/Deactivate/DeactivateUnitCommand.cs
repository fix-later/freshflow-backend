using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Deactivate;

public record DeactivateUnitCommand(Guid Id) : IRequest<Result<UnitDto>>;

using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Update;

public sealed record UpdatePackingCodeCommand(
    Guid Id,
    string Code,
    string? Description,
    decimal CapacityKg) : IRequest<Result<PackingCodeDto>>;

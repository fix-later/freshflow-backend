using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Create;

public sealed record CreatePackingCodeCommand(
    string Code,
    string? Description,
    decimal CapacityKg) : IRequest<Result<PackingCodeDto>>;

using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Deactivate;

public sealed record DeactivatePackingCodeCommand(Guid Id) : IRequest<Result<PackingCodeDto>>;

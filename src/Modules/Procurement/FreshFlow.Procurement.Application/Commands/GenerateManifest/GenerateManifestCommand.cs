using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.GenerateManifest;

public sealed record GenerateManifestCommand(Guid BatchId) : ICommand<ProcurementBatchDto>;

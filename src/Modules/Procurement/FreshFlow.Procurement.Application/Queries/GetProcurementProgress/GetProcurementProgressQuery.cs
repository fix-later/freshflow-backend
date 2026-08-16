using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementProgress;

public sealed record GetProcurementProgressQuery(
    DateOnly? Date,
    string? Status) : IQuery<ProcurementProgressDto>;

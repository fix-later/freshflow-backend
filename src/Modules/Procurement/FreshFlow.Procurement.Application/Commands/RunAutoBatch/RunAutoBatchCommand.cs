using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.RunAutoBatch;

public sealed record RunAutoBatchCommand(
    DateOnly? TargetDate,
    bool DryRun,
    bool Force) : ICommand<BatchingResult>;

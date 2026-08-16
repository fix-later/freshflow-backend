using FluentValidation;
using FreshFlow.Procurement.Application.Services;

namespace FreshFlow.Procurement.Application.Commands.RunAutoBatch;

internal sealed class RunAutoBatchCommandValidator : AbstractValidator<RunAutoBatchCommand>
{
    public RunAutoBatchCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.TargetDate)
            .Must(targetDate =>
                !targetDate.HasValue ||
                targetDate.Value >= ProcurementBatchCycle.GetLocalDate(timeProvider.GetUtcNow()))
            .WithMessage("'TargetDate' must not be in the past.");
    }
}

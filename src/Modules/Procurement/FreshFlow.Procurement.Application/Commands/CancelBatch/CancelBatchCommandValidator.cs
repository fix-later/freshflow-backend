using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.CancelBatch;

internal sealed class CancelBatchCommandValidator : AbstractValidator<CancelBatchCommand>
{
    public CancelBatchCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.Reason)
            .MaximumLength(500)
            .When(command => command.Reason is not null);
    }
}

using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.HandoverBatch;

internal sealed class HandoverBatchCommandValidator : AbstractValidator<HandoverBatchCommand>
{
    public HandoverBatchCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.AgentUserId).NotEmpty();
        RuleFor(command => command.HubId)
            .NotEqual(Guid.Empty)
            .When(command => command.HubId.HasValue);
    }
}

using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.AssignAgent;

internal sealed class AssignAgentCommandValidator : AbstractValidator<AssignAgentCommand>
{
    public AssignAgentCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.AgentUserId).NotEmpty();
    }
}
